// ── Imports ─────────────────────────────────────────────────────────────────
import * as YsmRenderer from './ysm-three-renderer.js';
import { dotnet } from './_framework/dotnet.js'

// ── DOM element references ──────────────────────────────────────────────────
const RESTORE_BTN_ID = 'ysm-restore-btn';
const restoreBtn = document.getElementById(RESTORE_BTN_ID);

// ── Three.js renderer functions ─────────────────────────────────────────────
globalThis.ysmInit                 = YsmRenderer.init;
globalThis.ysmSetViewportRect      = YsmRenderer.setViewportRect;
globalThis.ysmShowCanvas           = YsmRenderer.showCanvas;
globalThis.ysmHideCanvas           = YsmRenderer.hideCanvas;
globalThis.ysmLoadModelGeometry    = YsmRenderer.loadModelGeometry;
globalThis.ysmAddTextureData       = YsmRenderer.addTextureData;
globalThis.ysmClearScene           = YsmRenderer.clearScene;
globalThis.ysmResetCamera          = YsmRenderer.resetCamera;
globalThis.ysmSetCameraView        = YsmRenderer.setCameraView;
globalThis.ysmSetBackground        = YsmRenderer.setBackground;
globalThis.ysmSetComponentVisible  = YsmRenderer.setComponentVisible;
globalThis.ysmSetBoneVisible       = YsmRenderer.setBoneVisible;
globalThis.ysmLoadAnimationData    = YsmRenderer.loadAnimationData;
globalThis.ysmPlayAnimation        = YsmRenderer.playAnimation;
globalThis.ysmStopAnimation        = YsmRenderer.stopAnimation;
globalThis.ysmGetAnimationProgress = YsmRenderer.getAnimationProgress;
globalThis.ysmDispose              = YsmRenderer.dispose;

// ── Restore button helpers ──────────────────────────────────────────────────
globalThis.ysmShowRestoreBtn = () => {
    if (restoreBtn) restoreBtn.style.display = 'block';
};
globalThis.ysmHideRestoreBtn = () => {
    if (restoreBtn) restoreBtn.style.display = 'none';
};

// ── Android WebView bridge ──────────────────────────────────────────────────
globalThis.ysmOpenAndroidFile = () => {
    if (globalThis.YSMAndroid?.openModelFile) {
        globalThis.YSMAndroid.openModelFile();
        return true;
    }
    return false;
};

// ── Dotnet WASM runtime ─────────────────────────────────────────────────────
const { getAssemblyExports, getConfig, runMain } = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const exportsPromise = getAssemblyExports("YSMViewer.Browser.dll");

globalThis.ysmLoadAndroidFile = async (url) => {
    const response = await fetch(url, { cache: 'no-store' });
    if (!response.ok) throw new Error(`Failed to load Android file: ${response.status}`);

    const bytes = new Uint8Array(await response.arrayBuffer());
    const exports = await exportsPromise;
    await exports.YSMViewer.Rendering.ThreeJs.ThreeJsInterop.LoadAndroidFile(bytes);
};

// Wire up the restore button click to call back into C#
exportsPromise.then(exports => {
    const interop = exports.YSMViewer.Rendering.ThreeJs.ThreeJsInterop;
    if (restoreBtn && interop) {
        restoreBtn.addEventListener('click', () => {
            try { interop.OnRestoreButtonClicked(); }
            catch { /* button click failure is non-critical */ }
        });
    }
});

const config = getConfig();
await runMain(config.mainAssemblyName, [globalThis.location.href]);
