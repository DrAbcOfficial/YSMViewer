import * as YsmRenderer from './ysm-three-renderer.js';
import { dotnet } from './_framework/dotnet.js'

const is_browser = typeof window != "undefined";
if (!is_browser) throw new Error(`Expected to be running in a browser`);

globalThis.ysmInit = YsmRenderer.init;
globalThis.ysmSetViewportRect = YsmRenderer.setViewportRect;
globalThis.ysmShowCanvas = YsmRenderer.showCanvas;
globalThis.ysmHideCanvas = YsmRenderer.hideCanvas;
globalThis.ysmLoadModelGeometry = YsmRenderer.loadModelGeometry;
globalThis.ysmAddTextureData = YsmRenderer.addTextureData;
globalThis.ysmClearScene = YsmRenderer.clearScene;
globalThis.ysmResetCamera = YsmRenderer.resetCamera;
globalThis.ysmSetCameraView = YsmRenderer.setCameraView;
globalThis.ysmSetBackground = YsmRenderer.setBackground;
globalThis.ysmSetComponentVisible = YsmRenderer.setComponentVisible;
globalThis.ysmSetBoneVisible = YsmRenderer.setBoneVisible;
globalThis.ysmLoadAnimationData = YsmRenderer.loadAnimationData;
globalThis.ysmPlayAnimation = YsmRenderer.playAnimation;
globalThis.ysmStopAnimation = YsmRenderer.stopAnimation;
globalThis.ysmGetAnimationProgress = YsmRenderer.getAnimationProgress;
globalThis.ysmDispose = YsmRenderer.dispose;

// UI overlay button helpers
globalThis.ysmShowRestoreBtn = () => {
    const btn = document.getElementById('ysm-restore-btn');
    if (btn) btn.style.display = 'block';
};
globalThis.ysmHideRestoreBtn = () => {
    const btn = document.getElementById('ysm-restore-btn');
    if (btn) btn.style.display = 'none';
};
const { getAssemblyExports, getConfig, runMain } = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

// Wire up HTML overlay button click handlers to C#
getAssemblyExports("YSMViewer.Browser.dll").then(exports => {
    const interop = exports.YSMViewer.Rendering.ThreeJs.ThreeJsInterop;
    document.getElementById('ysm-restore-btn').addEventListener('click', () => {
        try { interop.OnRestoreButtonClicked(); } catch (e) { }
    });
});

const config = getConfig();
await runMain(config.mainAssemblyName, [globalThis.location.href]);
