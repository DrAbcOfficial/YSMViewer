import * as THREE from './three.module.min.js';
import { OrbitControls } from './OrbitControls.js';

// ── Constants ───────────────────────────────────────────────────────────────
const MIME_PNG  = 'image/png';
const MIME_JPEG = 'image/jpeg';
const MIME_WEBP = 'image/webp';
const MIME_GIF  = 'image/gif';
const MIME_BMP  = 'image/bmp';

const DEFAULT_BG_R = 0x1e;
const DEFAULT_BG_G = 0x1e;
const DEFAULT_BG_B = 0x1e;

const CAMERA_FOV        = 50;
const CAMERA_NEAR       = 0.1;
const CAMERA_FAR        = 5000;
const CAMERA_DISTANCE   = 30;

const DRAG_DAMPING_FACTOR = 0.08;
const MAX_PIXEL_RATIO     = 2;
const MAX_DELTA_TIME      = 0.1;

const CAMERA_FIT_PADDING  = 1.5;
const CAMERA_FIT_OFFSET   = 2;

// ── Module state ────────────────────────────────────────────────────────────
let scene         = null;
let camera        = null;
let renderer      = null;
let controls      = null;
let canvasElement = null;
let isSceneReady  = false;

let modelGroups  = new Map();
let boneGroups   = new Map();
let animationBoneGroups = new Map();
let textureCache = new Map();

let animMixer          = null;
let animClips          = [];
let currentAnimAction  = null;
let animLoopActive     = false;
let renderFramePending = false;

// ── Scene initialization ────────────────────────────────────────────────────

export function init(canvasId) {
    canvasElement = document.getElementById(canvasId);
    if (!canvasElement) {
        console.error('[YSM-Three] Canvas not found:', canvasId);
        return;
    }

    renderer = new THREE.WebGLRenderer({
        canvas: canvasElement,
        antialias: true,
        alpha: false
    });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio, MAX_PIXEL_RATIO));
    renderer.setClearColor(new THREE.Color(DEFAULT_BG_R / 255, DEFAULT_BG_G / 255, DEFAULT_BG_B / 255));
    renderer.shadowMap.enabled = false;
    renderer.outputColorSpace = THREE.SRGBColorSpace;

    scene = new THREE.Scene();
    scene.background = new THREE.Color(DEFAULT_BG_R / 255, DEFAULT_BG_G / 255, DEFAULT_BG_B / 255);

    const ambientLight = new THREE.AmbientLight(0xffffff, 1.0);
    scene.add(ambientLight);

    const directionalLight = new THREE.DirectionalLight(0xffffff, 0.6);
    directionalLight.position.set(1, 1.5, 0.5);
    scene.add(directionalLight);

    camera = new THREE.PerspectiveCamera(
        CAMERA_FOV,
        canvasElement.width / canvasElement.height,
        CAMERA_NEAR,
        CAMERA_FAR
    );
    camera.position.set(0, 0, CAMERA_DISTANCE);
    camera.lookAt(0, 0, 0);

    controls = new OrbitControls(camera, canvasElement);
    controls.enableDamping = true;
    controls.dampingFactor = DRAG_DAMPING_FACTOR;
    controls.target.set(0, 0, 0);
    controls.update();
    controls.addEventListener('change', requestRender);

    window.addEventListener('resize', onResize);

    console.log('[YSM-Three] Initialized');
}

// ── Viewport & canvas visibility ────────────────────────────────────────────

export function setViewportRect(x, y, width, height) {
    if (!canvasElement) return;

    canvasElement.style.position  = 'absolute';
    canvasElement.style.left      = x + 'px';
    canvasElement.style.top       = y + 'px';
    canvasElement.style.width     = width + 'px';
    canvasElement.style.height    = height + 'px';
    canvasElement.style.display   = 'block';
    canvasElement.style.zIndex    = '1';

    const restoreBtn = document.getElementById('ysm-restore-btn');
    if (restoreBtn) {
        restoreBtn.style.left = (x + width - 20) + 'px';
        restoreBtn.style.top  = (y + (height - 48) / 2) + 'px';
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
        const w = canvasElement.clientWidth  || window.innerWidth;
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

// ── Model loading ───────────────────────────────────────────────────────────

export function loadModelGeometry(specJson) {
    try {
        const spec = JSON.parse(specJson);

        clearSceneInternal();
        modelGroups.clear();
        boneGroups.clear();
        animationBoneGroups.clear();
        disposeAnimations();

        for (const model of spec.models || []) {
            buildModelComponent(model);
        }

        fitCameraToScene();
        isSceneReady = true;
        requestRender();

        console.log('[YSM-Three] Model loaded:', spec.models?.length ?? 0, 'components');
    } catch (err) {
        console.error('[YSM-Three] Failed to load model:', err);
    }
}

function buildModelComponent(model) {
    const modelGroup = new THREE.Group();
    modelGroup.name = model.id;
    modelGroup.userData = {
        componentId:    model.id,
        defaultVisible: model.defaultVisible !== false,
        textureId:      model.textureId,
        textureWidth:   model.textureWidth  ?? 64,
        textureHeight:  model.textureHeight ?? 64
    };
    modelGroup.visible = model.defaultVisible !== false;

    const material = getOrCreateMaterial(model.textureId);
    const bones = model.bones || [];

    for (const bone of bones) {
        const boneGroup = new THREE.Group();
        boneGroup.name = bone.name || bone.id;
        boneGroup.userData = {
            boneId:           bone.id,
            componentId:      model.id,
            initialPosition:  bone.localPosition ? [...bone.localPosition] : [0, 0, 0],
            initialRotation:  bone.localRotation ? [...bone.localRotation] : [0, 0, 0, 1]
        };
        setObjectTransform(boneGroup, bone.localPosition, bone.localRotation);
        boneGroups.set(bone.id, boneGroup);
        if (!animationBoneGroups.has(bone.name)) {
            animationBoneGroups.set(bone.name, boneGroup);
        }
    }

    for (const bone of bones) {
        const bg         = boneGroups.get(bone.id);
        const parentBone = bone.parentId ? boneGroups.get(bone.parentId) : null;
        (parentBone || modelGroup).add(bg);
    }

    for (const meshData of model.meshGroups || []) {
        const geometry = buildBufferGeometry(meshData);
        const mesh = new THREE.Mesh(geometry, material);
        mesh.name = meshData.id || meshData.boneId;
        mesh.userData = {
            boneId:      meshData.boneId,
            componentId: model.id
        };
        setObjectTransform(mesh, meshData.localPosition, meshData.localRotation);

        const parentBone = boneGroups.get(meshData.boneId);
        (parentBone || modelGroup).add(mesh);
    }

    scene.add(modelGroup);
    modelGroups.set(model.id, modelGroup);
}

// ── Geometry building ───────────────────────────────────────────────────────

function buildBufferGeometry(meshData) {
    const positions = new Float32Array(meshData.positions);
    const normals   = new Float32Array(meshData.normals);
    const uvs       = new Float32Array(meshData.uvs);
    const indices   = meshData.indices;

    const geom = new THREE.BufferGeometry();
    geom.setAttribute('position', new THREE.BufferAttribute(positions, 3));
    geom.setAttribute('normal',   new THREE.BufferAttribute(normals,   3));
    geom.setAttribute('uv',       new THREE.BufferAttribute(uvs,       2));
    geom.setIndex(indices);
    geom.computeBoundingSphere();
    geom.computeBoundingBox();

    return geom;
}

function setObjectTransform(obj, position, rotation) {
    if (position && position.length >= 3) {
        obj.position.set(position[0], position[1], position[2]);
    }
    if (rotation && rotation.length >= 4) {
        obj.quaternion.set(rotation[0], rotation[1], rotation[2], rotation[3]);
    }
}

// ── Materials & textures ────────────────────────────────────────────────────

function getOrCreateMaterial(textureId) {
    const cachedTex = textureCache.get(textureId);
    if (cachedTex) {
        return new THREE.MeshBasicMaterial({
            map:         cachedTex,
            side:        THREE.FrontSide,
            transparent: true,
            alphaTest:   0.1,
            depthWrite:  true,
        });
    }
    return new THREE.MeshBasicMaterial({
        color: 0xcccccc,
        side:  THREE.FrontSide,
    });
}

export function addTextureData(textureId, uint8Array) {
    const dataCopy = uint8Array.slice(0);
    loadTextureAsync(textureId, dataCopy);
}

async function loadTextureAsync(textureId, dataCopy) {
    try {
        const mimeType = detectImageMimeType(dataCopy);
        const blob     = new Blob([dataCopy], { type: mimeType });
        let imageBitmap;

        try {
            imageBitmap = await createImageBitmap(blob);
        } catch (err) {
            console.warn('[YSM-Three] createImageBitmap failed for', textureId, '- trying TextureLoader');
            return loadTextureViaLoader(textureId, blob);
        }

        const tex = new THREE.Texture(imageBitmap);
        tex.magFilter  = THREE.NearestFilter;
        tex.minFilter  = THREE.NearestFilter;
        tex.colorSpace = THREE.SRGBColorSpace;
        tex.needsUpdate = true;

        textureCache.set(textureId, tex);
        applyTextureToMaterials(textureId, tex);
        requestRender();

    } catch (err) {
        console.error('[YSM-Three] Failed to add texture:', textureId, err);
    }
}

async function loadTextureViaLoader(textureId, blob) {
    const url = URL.createObjectURL(blob);
    try {
        const tex = await new Promise((resolve, reject) => {
            new THREE.TextureLoader().load(url, resolve, undefined, reject);
        });
        tex.magFilter   = THREE.NearestFilter;
        tex.minFilter   = THREE.NearestFilter;
        tex.colorSpace  = THREE.SRGBColorSpace;
        tex.needsUpdate = true;

        textureCache.set(textureId, tex);
        applyTextureToMaterials(textureId, tex);
        requestRender();
    } catch (loaderErr) {
        console.error('[YSM-Three] TextureLoader also failed for', textureId, loaderErr);
    } finally {
        URL.revokeObjectURL(url);
    }
}

function applyTextureToMaterials(textureId, texture) {
    if (!scene) return;

    const newMat = new THREE.MeshBasicMaterial({
        map:          texture,
        color:        0xffffff,
        side:         THREE.FrontSide,
        transparent:  true,
        alphaTest:    0.1,
        depthWrite:   true,
    });

    scene.traverse((child) => {
        if (!child.isMesh || !child.material) return;

        const modelGroup = findComponentGroup(child);
        if (!modelGroup || modelGroup.userData.textureId !== textureId) return;

        if (Array.isArray(child.material)) {
            for (const m of child.material) {
                if (m !== newMat) m.dispose();
            }
        } else if (child.material !== newMat) {
            child.material.dispose();
        }
        child.material = newMat;
    });
}

function findComponentGroup(child) {
    let obj = child;
    while (obj) {
        const ud = obj.userData;
        if (ud && ud.textureId !== undefined && ud.componentId && modelGroups.has(ud.componentId)) {
            return obj;
        }
        obj = obj.parent;
    }
    return null;
}

function detectImageMimeType(data) {
    if (!data || data.length < 4) return MIME_PNG;

    if (data[0] === 0x89 && data[1] === 0x50 && data[2] === 0x4E && data[3] === 0x47) return MIME_PNG;  // \u0089PNG
    if (data[0] === 0xFF && data[1] === 0xD8 && data[2] === 0xFF)                     return MIME_JPEG; // \u00FF\u00D8\u00FF
    if (data[0] === 0x52 && data[1] === 0x49 && data[2] === 0x46 && data[3] === 0x46) return MIME_WEBP; // RIFF
    if (data[0] === 0x47 && data[1] === 0x49 && data[2] === 0x46)                     return MIME_GIF;  // GIF
    if (data[0] === 0x42 && data[1] === 0x4D)                                        return MIME_BMP;  // BM

    return MIME_PNG;
}

// ── Scene cleanup ───────────────────────────────────────────────────────────

export function clearScene() {
    clearSceneInternal();
    modelGroups.clear();
    boneGroups.clear();
    animationBoneGroups.clear();
    disposeAnimations();

    textureCache.forEach(t => t.dispose());
    textureCache.clear();

    isSceneReady = false;
    requestRender();
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

// ── Camera controls ─────────────────────────────────────────────────────────

export function setCameraView(viewName) {
    if (!isSceneReady) return;

    const box = computeSceneBoundingBox();
    if (!box) return;

    const center = new THREE.Vector3();
    box.getCenter(center);

    const size = new THREE.Vector3();
    box.getSize(size);
    const distance = Math.max(size.x, size.y, size.z) * CAMERA_FIT_PADDING + CAMERA_FIT_OFFSET;

    switch (viewName) {
    case 'front': camera.position.set(center.x, center.y, center.z + distance); break;
    case 'side':  camera.position.set(center.x + distance, center.y, center.z); break;
    case 'top':   camera.position.set(center.x, center.y + distance, center.z); break;
    default:      camera.position.set(center.x, center.y, center.z + distance); break;
    }

    camera.lookAt(center);
    controls.target.copy(center);
    controls.update();
    requestRender();
}

export function resetCamera() {
    if (!isSceneReady) return;
    fitCameraToScene();
    requestRender();
}

function fitCameraToScene() {
    const box = computeSceneBoundingBox();
    if (!box) return;

    const center = new THREE.Vector3();
    box.getCenter(center);

    const size = new THREE.Vector3();
    box.getSize(size);
    const distance = Math.max(size.x, size.y, size.z) * CAMERA_FIT_PADDING + CAMERA_FIT_OFFSET;

    camera.position.set(center.x, center.y, center.z + distance);
    camera.lookAt(center);
    controls.target.copy(center);
    controls.update();
}

function computeSceneBoundingBox() {
    const box = new THREE.Box3();
    scene.traverse((child) => {
        if (child.isMesh) box.expandByObject(child);
    });
    return box.isEmpty() ? null : box;
}

// ── Background & visibility ─────────────────────────────────────────────────

export function setBackground(r, g, b) {
    if (scene) {
        scene.background = new THREE.Color(r / 255, g / 255, b / 255);
        requestRender();
    }
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
        if (child.userData && child.userData.boneId === boneId) {
            child.visible = visible;
        }
    });
    requestRender();
}

// ── Animation loading ───────────────────────────────────────────────────────

export function loadAnimationData(json) {
    try {
        const animFile = JSON.parse(json);
        if (!animFile.animations) return;

        if (!animMixer) {
            animMixer = new THREE.AnimationMixer(scene);
        }

        for (const [animName, animData] of Object.entries(animFile.animations)) {
            if (animClips.find(c => c.name === animName)) continue;

            const tracks   = buildAnimationTracks(animData);
            const duration = animData.animation_length ?? animData.length ?? 1;

            if (tracks.length > 0) {
                animClips.push(new THREE.AnimationClip(animName, duration, tracks));
            }
        }

        console.log('[YSM-Three] Animations loaded:', animClips.length);
    } catch (err) {
        console.error('[YSM-Three] Failed to load animation data:', err);
    }
}

function buildAnimationTracks(animData) {
    const tracks  = [];
    const bones   = animData.bones || animData.animators || {};

    for (const [boneId, channels] of Object.entries(bones)) {
        const boneObj  = animationBoneGroups.get(boneId) || boneGroups.get(boneId);
        if (!boneObj) continue;

        const bonePath = getObjectPath(scene, boneObj);
        if (!bonePath) continue;

        if (channels.rotation) {
            const track = buildQuaternionTrack(bonePath + '.quaternion', channels.rotation, boneObj);
            if (track) tracks.push(track);
        }
        if (channels.position) {
            const track = buildPositionTrack(bonePath + '.position', channels.position, boneObj);
            if (track) tracks.push(track);
        }
        if (channels.scale) {
            const track = buildScaleTrack(bonePath + '.scale', channels.scale, boneObj);
            if (track) tracks.push(track);
        }
    }

    return tracks;
}

function buildQuaternionTrack(path, channel, boneObj) {
    const times  = [];
    const values = [];
    const kfs    = normalizeKeyframes(channel);
    const baseQuaternion = getInitialQuaternion(boneObj);

    for (const [t, val] of Object.entries(kfs)) {
        const vec = toVector3Array(val);
        const time = parseFloat(t);
        if (!vec || vec.length < 3 || Number.isNaN(time)) continue;
        times.push(time);

        const euler = new THREE.Euler(
            -vec[0] * Math.PI / 180,
            -vec[1] * Math.PI / 180,
             vec[2] * Math.PI / 180,
            'XYZ'
        );
        const q = new THREE.Quaternion().setFromEuler(euler);
        q.premultiply(baseQuaternion);
        values.push(q.x, q.y, q.z, q.w);
    }

    return times.length > 0
        ? new THREE.QuaternionKeyframeTrack(path, times, values)
        : null;
}

function buildPositionTrack(path, channel, boneObj) {
    const times  = [];
    const values = [];
    const kfs    = normalizeKeyframes(channel);
    const basePosition = getInitialPosition(boneObj);

    for (const [t, val] of Object.entries(kfs)) {
        const vec = toVector3Array(val);
        const time = parseFloat(t);
        if (!vec || vec.length < 3 || Number.isNaN(time)) continue;
        times.push(time);
        values.push(
            basePosition.x - vec[0] / 16,
            basePosition.y + vec[1] / 16,
            basePosition.z + vec[2] / 16);
    }

    return times.length > 0
        ? new THREE.VectorKeyframeTrack(path, times, values)
        : null;
}

function buildScaleTrack(path, channel, boneObj) {
    const times  = [];
    const values = [];
    const kfs    = normalizeKeyframes(channel);
    const baseScale = getInitialScale(boneObj);

    for (const [t, val] of Object.entries(kfs)) {
        const vec = toVector3Array(val);
        const time = parseFloat(t);
        if (!vec || vec.length < 3 || Number.isNaN(time)) continue;
        times.push(time);
        values.push(baseScale.x * vec[0], baseScale.y * vec[1], baseScale.z * vec[2]);
    }

    return times.length > 0
        ? new THREE.VectorKeyframeTrack(path, times, values)
        : null;
}

function normalizeKeyframes(channel) {
    if (Array.isArray(channel) || typeof channel === 'number') {
        return { 0: channel };
    }
    if (!channel || typeof channel !== 'object') {
        return {};
    }
    return channel;
}

function toVector3Array(value) {
    if (Array.isArray(value)) return value;
    if (typeof value === 'number') return [value, value, value];
    if (typeof value === 'string') {
        const parsed = parseFloat(value);
        return Number.isNaN(parsed) ? null : [parsed, parsed, parsed];
    }
    if (value && typeof value === 'object') {
        if (value.post !== undefined) return toVector3Array(value.post);
        if (value.pre !== undefined) return toVector3Array(value.pre);
    }
    return null;
}

function getInitialPosition(boneObj) {
    const ip = boneObj.userData ? boneObj.userData.initialPosition : null;
    return ip && ip.length >= 3
        ? new THREE.Vector3(ip[0], ip[1], ip[2])
        : new THREE.Vector3();
}

function getInitialQuaternion(boneObj) {
    const ir = boneObj.userData ? boneObj.userData.initialRotation : null;
    return ir && ir.length >= 4
        ? new THREE.Quaternion(ir[0], ir[1], ir[2], ir[3])
        : new THREE.Quaternion();
}

function getInitialScale(boneObj) {
    const is = boneObj.userData ? boneObj.userData.initialScale : null;
    return is && is.length >= 3
        ? new THREE.Vector3(is[0], is[1], is[2])
        : new THREE.Vector3(1, 1, 1);
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

// ── Animation playback ──────────────────────────────────────────────────────

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

    startAnimLoop();
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

    stopAnimLoop();
    resetBoneTransforms();
    requestRender();
}

function resetBoneTransforms() {
    for (const bg of boneGroups.values()) {
        const ip = bg.userData ? bg.userData.initialPosition : null;
        const ir = bg.userData ? bg.userData.initialRotation : null;

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
}

export function getAnimationProgress() {
    if (!animMixer || !currentAnimAction) {
        return JSON.stringify({ time: 0, duration: 0 });
    }
    const clip = currentAnimAction.getClip();
    return JSON.stringify({
        time:     animMixer.time,
        duration: clip ? clip.duration : 0
    });
}

// ── Animation loop ──────────────────────────────────────────────────────────

function startAnimLoop() {
    if (animLoopActive) return;
    animLoopActive = true;

    let lastTime = performance.now();

    function tick() {
        if (!animLoopActive) return;

        try {
            const now = performance.now();
            const dt  = Math.min((now - lastTime) / 1000, MAX_DELTA_TIME);
            lastTime  = now;

            if (animMixer) animMixer.update(dt);
            if (controls)  controls.update();

            if (renderer && scene && camera) {
                renderer.render(scene, camera);
            }

            requestAnimationFrame(tick);
        } catch (e) {
            console.error('[YSM-Three] Animation tick error:', e);
            stopAnimLoop();
        }
    }

    requestAnimationFrame(tick);
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
    animLoopActive = false;
}

// ── Render scheduling ───────────────────────────────────────────────────────

function requestRender() {
    if (animLoopActive) return;
    if (renderFramePending) return;

    renderFramePending = true;

    requestAnimationFrame(() => {
        renderFramePending = false;
        if (controls) controls.update();
        if (renderer && scene && camera) {
            renderer.render(scene, camera);
        }
    });
}

function onResize() {
    requestRender();
}

// ── Teardown ────────────────────────────────────────────────────────────────

export function dispose() {
    if (controls) {
        controls.dispose();
        controls = null;
    }

    disposeAnimations();
    clearSceneInternal();

    textureCache.forEach(t => t.dispose());
    textureCache.clear();

    modelGroups.clear();
    boneGroups.clear();

    if (renderer) {
        renderer.dispose();
        renderer = null;
    }

    window.removeEventListener('resize', onResize);

    scene  = null;
    camera = null;
    isSceneReady = false;
}
