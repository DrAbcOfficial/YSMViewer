import * as THREE from 'three';
import { OrbitControls } from 'three/addons/controls/OrbitControls.js';

let scene, camera, renderer, controls;
let canvasElement;
let modelGroups = new Map();
let textureCache = new Map();
let isAutoRotating = true;
let isSceneReady = false;
let renderRequested = false;
let animationId = null;

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
    startRenderLoop();

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
}

export function showCanvas() {
    if (canvasElement) canvasElement.style.display = 'block';
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

            for (const meshData of model.meshGroups || []) {
                const geometry = buildBufferGeometry(meshData);
                const mesh = new THREE.Mesh(geometry, material);
                mesh.name = meshData.id || meshData.boneId;
                mesh.userData = {
                    boneId: meshData.boneId,
                    componentId: model.id
                };
                modelGroup.add(mesh);
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
        const blob = new Blob([uint8Array], { type: 'image/png' });
        const url = URL.createObjectURL(blob);
        const texture = new THREE.TextureLoader().load(url, (tex) => {
            tex.magFilter = THREE.NearestFilter;
            tex.minFilter = THREE.NearestFilter;
            tex.colorSpace = THREE.SRGBColorSpace;
            tex.needsUpdate = true;
            textureCache.set(textureId, tex);
            applyTextureToMaterials(textureId, tex);
            requestRender();
            URL.revokeObjectURL(url);
        }, undefined, (err) => {
            console.error('[YSM-Three] Texture load error:', textureId, err);
            URL.revokeObjectURL(url);
        });
    } catch (err) {
        console.error('[YSM-Three] Failed to add texture:', textureId, err);
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

export function setAutoRotate(enabled) {
    isAutoRotating = enabled;
    if (enabled) requestRender();
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
    stopRenderLoop();
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

function getOrCreateMaterial(textureId) {
    const cachedTex = textureCache.get(textureId);
    if (cachedTex) {
        return new THREE.MeshStandardMaterial({
            map: cachedTex,
            roughness: 0.9,
            metalness: 0.0,
            side: THREE.FrontSide,
            alphaTest: 0.5,
            transparent: false,
        });
    }
    return new THREE.MeshStandardMaterial({
        roughness: 0.9,
        metalness: 0.0,
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
                    mat.forEach(m => m.map = texture);
                } else {
                    mat.map = texture;
                    mat.needsUpdate = true;
                }
            }
        }
    });
}

function findComponentGroup(child) {
    let obj = child;
    while (obj) {
        if (obj.userData?.componentId && modelGroups.has(obj.userData.componentId)) {
            return obj;
        }
        obj = obj.parent;
    }
    return null;
}

function clearSceneInternal() {
    if (!scene) return;
    while (scene.children.length > 0) {
        disposeObject(scene.children[0]);
    }
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

function startRenderLoop() {
    function animate() {
        animationId = requestAnimationFrame(animate);
        if (isAutoRotating && isSceneReady) {
            if (controls) controls.update();
            if (renderer && scene && camera) {
                renderer.render(scene, camera);
            }
        }
    }
    animate();
}

function stopRenderLoop() {
    if (animationId) {
        cancelAnimationFrame(animationId);
        animationId = null;
    }
}

function requestRender() {
    if (!renderRequested) {
        renderRequested = true;
        requestAnimationFrame(() => {
            renderRequested = false;
            if (controls) controls.update();
            if (renderer && scene && camera) {
                renderer.render(scene, camera);
            }
        });
    }
}
