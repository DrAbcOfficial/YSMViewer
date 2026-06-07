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
globalThis.ysmSetCameraView = YsmRenderer.setCameraView;
globalThis.ysmSetBackground = YsmRenderer.setBackground;
globalThis.ysmSetComponentVisible = YsmRenderer.setComponentVisible;
globalThis.ysmSetBoneVisible = YsmRenderer.setBoneVisible;
globalThis.ysmLoadAnimationData = YsmRenderer.loadAnimationData;
globalThis.ysmPlayAnimation = YsmRenderer.playAnimation;
globalThis.ysmStopAnimation = YsmRenderer.stopAnimation;
globalThis.ysmGetAnimationProgress = YsmRenderer.getAnimationProgress;
globalThis.ysmDispose = YsmRenderer.dispose;

const dotnetRuntime = await dotnet
    .withDiagnosticTracing(false)
    .withApplicationArgumentsFromQuery()
    .create();

const config = dotnetRuntime.getConfig();

await dotnetRuntime.runMain(config.mainAssemblyName, [globalThis.location.href]);
