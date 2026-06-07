import * as THREE from './three.module.min.js';
import { OrbitControls } from './OrbitControls.js';

let scene, camera, renderer, controls;
let canvasElement;
let modelGroups = new Map();
let textureCache = new Map();
let isSceneReady = false;
let renderFramePending = false;

export function init(canvasId) {
    canvasElement = document.getElementById(canvasId);
    if (!canvasElement) {
        console.error('[YSM-Three] Canvas not found:', canvasId);
        return;
    }

    renderer = new THREE.WebGLRenderer({ canvas: canvasElement, antialias: true, alpha: false });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
    renderer.setClearColor(0x1e1e1e);
    renderer.shadowMap.enabled = false;
    renderer.outputColorSpace = THREE.SRGBColorSpace;

    scene = new THREE.Scene();
    scene.background = new THREE.Color(0x1e1e1e);

    const ambientLight = new THREE.AmbientLight(0xffffff, 1.0);
    scene.add(ambientLight);

    const directionalLight = new THREE.DirectionalLight(0xffffff, 0.6);
    directionalLight.position.set(1, 1.5, 0.5);
    scene.add(directionalLight);

    camera = new THREE.PerspectiveCamera(50, canvasElement.width / canvasElement.height, 0.1, 5000);
    camera.position.set(0, 0, 30);
    camera.lookAt(0, 0, 0);

    controls = new OrbitControls(camera, canvasElement);
    controls.enableDamping = true;
    controls.dampingFactor = 0.08;
    controls.target.set(0, 0, 0);
    controls.update();
    controls.addEventListener('change', requestRender);

    window.addEventListener('resize', onResize);

    console.log('[YSM-Three] Initialized');
}

export function setViewportRect(x, y, width, height) {
    if (!canvasElement) return;
    canvasElement.style.position = 'absolute';
    canvasElement.style.left = x + 'px';
    canvasElement.style.top = y + 'px';
    canvasElement.style.width = width + 'px';
    canvasElement.style.height = height + 'px';
    canvasElement.style.display = 'block';
    canvasElement.style.zIndex = '1';

    if (renderer) {
        renderer.setSize(width, height, false);
        camera.aspect = width / Math.max(height, 1);
        camera.updateProjectionMatrix();
    }
    requestRender();
}

export function showCanvas() {
    if (!canvasElement) return;
    canvasElement.style.display = 'block';
    if (renderer) {
        const w = canvasElement.clientWidth || window.innerWidth;
        const h = canvasElement.clientHeight || window.innerHeight;
        if (w > 0 && h > 0) {
            renderer.setSize(w, h, false);
            if (camera) {
                camera.aspect = w / h;
                camera.updateProjectionMatrix();
            }
        }
    }
}

export function hideCanvas() {
    if (canvasElement) canvasElement.style.display = 'none';
}

export function loadModelGeometry(specJson) {
    try {
        const spec = JSON.parse(specJson);
        clearSceneInternal();
        modelGroups.clear();

        for (const model of spec.models || []) {
            const modelGroup = new THREE.Group();
            modelGroup.name = model.id;
            modelGroup.userData = {
                componentId: model.id,
                defaultVisible: model.defaultVisible !== false,
                textureId: model.textureId,
                textureWidth: model.textureWidth || 64,
                textureHeight: model.textureHeight || 64
            };
            modelGroup.visible = model.defaultVisible !== false;

            const material = getOrCreateMaterial(model.textureId);
            const boneGroups = new Map();

            for (const bone of model.bones || []) {
                const boneGroup = new THREE.Group();
                boneGroup.name = bone.name || bone.id;
                boneGroup.userData = {
                    boneId: bone.id,
                    componentId: model.id
                };
                setObjectTransform(boneGroup, bone.localPosition, bone.localRotation);
                boneGroups.set(bone.id, boneGroup);
            }

            for (const bone of model.bones || []) {
                const boneGroup = boneGroups.get(bone.id);
                const parentGroup = bone.parentId ? boneGroups.get(bone.parentId) : null;
                (parentGroup || modelGroup).add(boneGroup);
            }

            for (const meshData of model.meshGroups || []) {
                const geometry = buildBufferGeometry(meshData);
                const mesh = new THREE.Mesh(geometry, material);
                mesh.name = meshData.id || meshData.boneId;
                mesh.userData = {
                    boneId: meshData.boneId,
                    componentId: model.id
                };
                setObjectTransform(mesh, meshData.localPosition, meshData.localRotation);

                const boneGroup = boneGroups.get(meshData.boneId);
                (boneGroup || modelGroup).add(mesh);
            }

            scene.add(modelGroup);
            modelGroups.set(model.id, modelGroup);
        }

        fitCameraToScene();
        isSceneReady = true;
        requestRender();
        console.log('[YSM-Three] Model loaded:', spec.models?.length || 0, 'components');
    } catch (err) {
        console.error('[YSM-Three] Failed to load model:', err);
    }
}

export function addTextureData(textureId, uint8Array) {
    try {
        const mimeType = detectImageMimeType(uint8Array);
        const blob = new Blob([uint8Array], { type: mimeType });
        const url = URL.createObjectURL(blob);

        const onTextureLoaded = (tex) => {
            tex.magFilter = THREE.NearestFilter;
            tex.minFilter = THREE.NearestFilter;
            tex.colorSpace = THREE.SRGBColorSpace;
            tex.needsUpdate = true;
            textureCache.set(textureId, tex);
            applyTextureToMaterials(textureId, tex);
            requestRender();
            URL.revokeObjectURL(url);
        };

        const onTextureError = (err) => {
            console.warn('[YSM-Three] TextureLoader error for', textureId, '- trying createImageBitmap fallback');
            loadTextureViaImageBitmap(blob, textureId).then((tex) => {
                if (tex) {
                    onTextureLoaded(tex);
                }
            });
            URL.revokeObjectURL(url);
        };

        new THREE.TextureLoader().load(url, onTextureLoaded, undefined, onTextureError);
    } catch (err) {
        console.error('[YSM-Three] Failed to add texture:', textureId, err);
    }
}

async function loadTextureViaImageBitmap(blob, textureId) {
    try {
        const imageBitmap = await createImageBitmap(blob);
        const tex = new THREE.Texture(imageBitmap);
        tex.magFilter = THREE.NearestFilter;
        tex.minFilter = THREE.NearestFilter;
        tex.colorSpace = THREE.SRGBColorSpace;
        tex.needsUpdate = true;
        return tex;
    } catch (err) {
        console.error('[YSM-Three] createImageBitmap fallback also failed for', textureId, err);
        return null;
    }
}

export function clearScene() {
    clearSceneInternal();
    modelGroups.clear();
    textureCache.forEach(t => t.dispose());
    textureCache.clear();
    isSceneReady = false;
    requestRender();
}

export function setCameraView(viewName) {
    if (!isSceneReady) return;

    const box = new THREE.Box3();
    let hasContent = false;
    scene.traverse((child) => {
        if (child.isMesh) {
            box.expandByObject(child);
            hasContent = true;
        }
    });

    if (!hasContent) return;

    const center = new THREE.Vector3();
    box.getCenter(center);
    const size = new THREE.Vector3();
    box.getSize(size);
    const distance = Math.max(size.x, size.y, size.z) * 1.5 + 2;

    switch (viewName) {
    case 'front':
        camera.position.set(center.x, center.y, center.z + distance);
        break;
    case 'side':
        camera.position.set(center.x + distance, center.y, center.z);
        break;
    case 'top':
        camera.position.set(center.x, center.y + distance, center.z);
        break;
    default:
        camera.position.set(center.x, center.y, center.z + distance);
    }
    camera.lookAt(center);
    controls.target.copy(center);
    controls.update();
    requestRender();
}

export function setBackground(r, g, b) {
    const color = new THREE.Color(r / 255, g / 255, b / 255);
    if (scene) scene.background = color;
    requestRender();
}

export function setComponentVisible(componentId, visible) {
    const group = modelGroups.get(componentId);
    if (group) {
        group.visible = visible;
        requestRender();
    }
}

export function setBoneVisible(boneId, visible) {
    if (!scene) return;
    scene.traverse((child) => {
        if (child.userData?.boneId === boneId) {
            child.visible = visible;
        }
    });
    requestRender();
}

export function dispose() {
    if (controls) controls.dispose();
    clearSceneInternal();
    textureCache.forEach(t => t.dispose());
    textureCache.clear();
    if (renderer) {
        renderer.dispose();
        renderer = null;
    }
    window.removeEventListener('resize', onResize);
    scene = null;
    camera = null;
}

function buildBufferGeometry(meshData) {
    const positions = new Float32Array(meshData.positions);
    const normals = new Float32Array(meshData.normals);
    const uvs = new Float32Array(meshData.uvs);
    const indices = meshData.indices;

    const geom = new THREE.BufferGeometry();
    geom.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geom.setAttribute('normal', new THREE.BufferAttribute(normals, 3));
    geom.setAttribute('uv', new THREE.BufferAttribute(uvs, 2));
    geom.setIndex(indices);
    geom.computeBoundingSphere();
    geom.computeBoundingBox();
    return geom;
}

function setObjectTransform(obj, position, rotation) {
    if (position?.length >= 3) {
        obj.position.set(position[0], position[1], position[2]);
    }
    if (rotation?.length >= 4) {
        obj.quaternion.set(rotation[0], rotation[1], rotation[2], rotation[3]);
    }
}

function getOrCreateMaterial(textureId) {
    const cachedTex = textureCache.get(textureId);
    if (cachedTex) {
        return new THREE.MeshBasicMaterial({
            map: cachedTex,
            side: THREE.FrontSide,
            alphaTest: 0.5,
            transparent: false,
        });
    }
    return new THREE.MeshBasicMaterial({
        color: 0xcccccc,
        side: THREE.FrontSide,
    });
}

function applyTextureToMaterials(textureId, texture) {
    if (!scene) return;
    scene.traverse((child) => {
        if (child.isMesh && child.material) {
            const mat = child.material;
            const modelGroup = findComponentGroup(child);
            if (modelGroup && modelGroup.userData.textureId === textureId) {
                if (Array.isArray(mat)) {
                    mat.forEach((m) => {
                        m.map = texture;
                        m.color?.set(0xffffff);
                        m.needsUpdate = true;
                    });
                } else {
                    mat.map = texture;
                    mat.color?.set(0xffffff);
                    mat.needsUpdate = true;
                }
            }
        }
    });
}

function findComponentGroup(child) {
    let obj = child;
    while (obj) {
        if (obj.userData?.textureId !== undefined && obj.userData?.componentId && modelGroups.has(obj.userData.componentId)) {
            return obj;
        }
        obj = obj.parent;
    }
    return null;
}

function detectImageMimeType(data) {
    if (!data || data.length < 4) return 'image/png';
    if (data[0] === 0x89 && data[1] === 0x50 && data[2] === 0x4E && data[3] === 0x47) return 'image/png';
    if (data[0] === 0xFF && data[1] === 0xD8 && data[2] === 0xFF) return 'image/jpeg';
    if (data[0] === 0x52 && data[1] === 0x49 && data[2] === 0x46 && data[3] === 0x46) return 'image/webp';
    if (data[0] === 0x47 && data[1] === 0x49 && data[2] === 0x46) return 'image/gif';
    if (data[0] === 0x42 && data[1] === 0x4D) return 'image/bmp';
    return 'image/png';
}

function clearSceneInternal() {
    if (!scene) return;
    modelGroups.forEach(group => disposeObject(group));
}

function disposeObject(obj) {
    if (obj.geometry) obj.geometry.dispose();
    if (obj.material) {
        if (Array.isArray(obj.material)) {
            obj.material.forEach(m => m.dispose());
        } else {
            obj.material.dispose();
        }
    }
    while (obj.children.length > 0) {
        disposeObject(obj.children[0]);
    }
    if (obj.parent) obj.parent.remove(obj);
}

function fitCameraToScene() {
    const box = new THREE.Box3();
    scene.traverse((child) => {
        if (child.isMesh) box.expandByObject(child);
    });

    if (box.isEmpty()) return;

    const center = new THREE.Vector3();
    box.getCenter(center);
    const size = new THREE.Vector3();
    box.getSize(size);
    const distance = Math.max(size.x, size.y, size.z) * 1.5 + 2;

    camera.position.set(center.x, center.y, center.z + distance);
    camera.lookAt(center);
    controls.target.copy(center);
    controls.update();
}

function onResize() {
    requestRender();
}

function requestRender() {
    if (!renderFramePending) {
        renderFramePending = true;
        requestAnimationFrame(() => {
            renderFramePending = false;
            if (controls) controls.update();
            if (renderer && scene && camera) {
                renderer.render(scene, camera);
            }
        });
    }
}
