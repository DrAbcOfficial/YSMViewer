import * as THREE from './three.module.min.js';
import { OrbitControls } from './OrbitControls.js';

let scene, camera, renderer, controls;
let canvasElement;
let modelGroups = new Map();
let boneGroups = new Map();
let textureCache = new Map();
let isSceneReady = false;
let renderFramePending = false;
let animMixer, animClips, currentAnimAction;
let _currentAnimName = null;
let _currentAnimTime = 0;
let animLoopActive = false;

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

    const restoreBtn = document.getElementById('ysm-restore-btn');
    if (restoreBtn) {
        restoreBtn.style.left = (x + width - 20) + 'px';
        restoreBtn.style.top = (y + (height - 48) / 2) + 'px';
    }
    const fab = document.getElementById('ysm-fab');
    if (fab) {
        fab.style.left = (x + width - 72) + 'px';
        fab.style.top = (y + height - 72) + 'px';
    }

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
        boneGroups.clear();

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

            for (const bone of model.bones || []) {
                const boneGroup = new THREE.Group();
                boneGroup.name = bone.name || bone.id;
                boneGroup.userData = {
                    boneId: bone.id,
                    componentId: model.id,
                    initialPosition: bone.localPosition ? [...bone.localPosition] : [0, 0, 0],
                    initialRotation: bone.localRotation ? [...bone.localRotation] : [0, 0, 0, 1]
                };
                setObjectTransform(boneGroup, bone.localPosition, bone.localRotation);
                boneGroups.set(bone.id, boneGroup);
            }

            for (const bone of model.bones || []) {
                const bg = boneGroups.get(bone.id);
                const parentGroup = bone.parentId ? boneGroups.get(bone.parentId) : null;
                (parentGroup || modelGroup).add(bg);
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
    const dataCopy = uint8Array.slice(0);
    loadTextureAsync(textureId, dataCopy);
}

async function loadTextureAsync(textureId, dataCopy) {
    try {
        const mimeType = detectImageMimeType(dataCopy);
        const blob = new Blob([dataCopy], { type: mimeType });
        let imageBitmap;
        try {
            imageBitmap = await createImageBitmap(blob);
        } catch (err) {
            console.warn('[YSM-Three] createImageBitmap failed for', textureId, '- trying TextureLoader');
            const url = URL.createObjectURL(blob);
            try {
                const tex = await new Promise((resolve, reject) => {
                    new THREE.TextureLoader().load(url, resolve, undefined, reject);
                });
                tex.magFilter = THREE.NearestFilter;
                tex.minFilter = THREE.NearestFilter;
                tex.colorSpace = THREE.SRGBColorSpace;
                tex.needsUpdate = true;
                textureCache.set(textureId, tex);
                applyTextureToMaterials(textureId, tex);
                requestRender();
                URL.revokeObjectURL(url);
                return;
            } catch (loaderErr) {
                console.error('[YSM-Three] TextureLoader also failed for', textureId, loaderErr);
                URL.revokeObjectURL(url);
                return;
            }
        }

        const tex = new THREE.Texture(imageBitmap);
        tex.magFilter = THREE.NearestFilter;
        tex.minFilter = THREE.NearestFilter;
        tex.colorSpace = THREE.SRGBColorSpace;
        tex.needsUpdate = true;

        textureCache.set(textureId, tex);
        applyTextureToMaterials(textureId, tex);
        requestRender();
    } catch (err) {
        console.error('[YSM-Three] Failed to add texture:', textureId, err);
    }
}

export function clearScene() {
    clearSceneInternal();
    modelGroups.clear();
    boneGroups.clear();
    disposeAnimations();
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

export function loadAnimationData(json) {
    try {
        const animFile = JSON.parse(json);
        if (!animFile.animations) return;
        if (!animMixer) {
            animMixer = new THREE.AnimationMixer(scene);
        }
        animClips = animClips || [];

        for (const [animName, animData] of Object.entries(animFile.animations)) {
            if (animClips.find(c => c.name === animName)) continue;

            const tracks = [];
            const bones = animData.bones || animData.animators || {};
            const duration = animData.animation_length || animData.length || 1;

            for (const [boneId, channels] of Object.entries(bones)) {
                const boneObj = boneGroups.get(boneId);
                if (!boneObj) continue;
                const bonePath = getObjectPath(scene, boneObj);
                if (!bonePath) continue;

                if (channels.rotation) {
                    const times = [];
                    const values = [];
                    const kfs = typeof channels.rotation === 'object' ? channels.rotation : {};
                    for (const [t, val] of Object.entries(kfs)) {
                        const time = parseFloat(t);
                        times.push(time);
                        if (Array.isArray(val) && val.length >= 3) {
                            const euler = new THREE.Euler(
                                -val[0] * Math.PI / 180,
                                -val[1] * Math.PI / 180,
                                val[2] * Math.PI / 180,
                                'XYZ'
                            );
                            const q = new THREE.Quaternion().setFromEuler(euler);
                            values.push(q.x, q.y, q.z, q.w);
                        }
                    }
                    if (times.length > 0) {
                        tracks.push(new THREE.QuaternionKeyframeTrack(
                            bonePath + '.quaternion',
                            times, values
                        ));
                    }
                }

                if (channels.position) {
                    const times = [];
                    const values = [];
                    const kfs = typeof channels.position === 'object' ? channels.position : {};
                    for (const [t, val] of Object.entries(kfs)) {
                        const time = parseFloat(t);
                        times.push(time);
                        if (Array.isArray(val) && val.length >= 3) {
                            values.push(-val[0] / 16, val[1] / 16, val[2] / 16);
                        }
                    }
                    if (times.length > 0) {
                        tracks.push(new THREE.VectorKeyframeTrack(
                            bonePath + '.position',
                            times, values
                        ));
                    }
                }

                if (channels.scale) {
                    const times = [];
                    const values = [];
                    const kfs = typeof channels.scale === 'object' ? channels.scale : {};
                    for (const [t, val] of Object.entries(kfs)) {
                        const time = parseFloat(t);
                        times.push(time);
                        if (Array.isArray(val) && val.length >= 3) {
                            values.push(val[0], val[1], val[2]);
                        }
                    }
                    if (times.length > 0) {
                        tracks.push(new THREE.VectorKeyframeTrack(
                            bonePath + '.scale',
                            times, values
                        ));
                    }
                }
            }

            if (tracks.length > 0) {
                const clip = new THREE.AnimationClip(animName, duration, tracks);
                animClips.push(clip);
            }
        }
        console.log('[YSM-Three] Animations loaded:', animClips.length);
    } catch (err) {
        console.error('[YSM-Three] Failed to load animation data:', err);
    }
}

function getObjectPath(root, target) {
    const visited = new Set();
    function search(node) {
        if (node === target) return '';
        if (visited.has(node)) return null;
        visited.add(node);
        for (let i = 0; i < node.children.length; i++) {
            if (node.children[i] === target) {
                return '.children[' + i + ']';
            }
            const sub = search(node.children[i]);
            if (sub !== null) {
                return '.children[' + i + ']' + sub;
            }
        }
        return null;
    }
    return search(root);
}

function findBoneIndex(boneId) {
    return -1;
}

export function playAnimation(name) {
    if (!animMixer || !animClips) return;
    const clip = animClips.find(c => c.name === name);
    if (!clip) return;

    if (currentAnimAction) {
        currentAnimAction.stop();
    }
    currentAnimAction = animMixer.clipAction(clip);
    currentAnimAction.play();
    animMixer.setTime(0);
    _currentAnimName = name;
    _currentAnimTime = 0;
    startAnimLoop();
}

export function getAnimationProgress() {
    if (!animMixer || !currentAnimAction) return JSON.stringify({ time: 0, duration: 0 });
    const clip = currentAnimAction.getClip();
    return JSON.stringify({ time: animMixer.time, duration: clip ? clip.duration : 0 });
}

export function stopAnimation() {
    if (currentAnimAction) {
        currentAnimAction.stop();
        currentAnimAction = null;
    }
    if (animMixer) {
        animMixer.stopAllAction();
        animMixer.setTime(0);
    }
    _currentAnimName = null;
    _currentAnimTime = 0;
    stopAnimLoop();
    for (const bg of boneGroups.values()) {
        const ip = bg.userData?.initialPosition;
        const ir = bg.userData?.initialRotation;
        if (ip && ip.length >= 3) {
            bg.position.set(ip[0], ip[1], ip[2]);
        } else {
            bg.position.set(0, 0, 0);
        }
        if (ir && ir.length >= 4) {
            bg.quaternion.set(ir[0], ir[1], ir[2], ir[3]);
        } else {
            bg.quaternion.identity();
        }
        bg.scale.set(1, 1, 1);
    }
    requestRender();
}

function startAnimLoop() {
    if (animLoopActive) return;
    animLoopActive = true;
    let lastTime = performance.now();
    function tick() {
        if (!animLoopActive) return;
        try {
            const now = performance.now();
            const dt = Math.min((now - lastTime) / 1000, 0.1);
            lastTime = now;

            if (animMixer) {
                animMixer.update(dt);
                _currentAnimTime = animMixer.time;
            }
            if (controls) controls.update();
            if (renderer && scene && camera) {
                renderer.render(scene, camera);
            }
        } catch (e) {
            console.error('[YSM-Three] Animation tick error:', e);
            stopAnimLoop();
            return;
        }
        requestAnimationFrame(tick);
    }
    tick();
}

function stopAnimLoop() {
    animLoopActive = false;
}

function disposeAnimations() {
    if (currentAnimAction) {
        currentAnimAction.stop();
        currentAnimAction = null;
    }
    if (animMixer) {
        animMixer.stopAllAction();
        animMixer = null;
    }
    animClips = [];
    _currentAnimName = null;
    _currentAnimTime = 0;
    animLoopActive = false;
}

export function dispose() {
    if (controls) controls.dispose();
    disposeAnimations();
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
    boneGroups.clear();
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
            transparent: true,
        });
    }
    return new THREE.MeshBasicMaterial({
        color: 0xcccccc,
        side: THREE.FrontSide,
    });
}

function applyTextureToMaterials(textureId, texture) {
    if (!scene) return;
    const newMat = new THREE.MeshBasicMaterial({
        map: texture,
        color: 0xffffff,
        side: THREE.FrontSide,
        transparent: true,
    });
    scene.traverse((child) => {
        if (child.isMesh && child.material) {
            const modelGroup = findComponentGroup(child);
            if (modelGroup && modelGroup.userData.textureId === textureId) {
                child.material = newMat;
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
    if (animLoopActive) return;
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
