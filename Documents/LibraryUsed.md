# Library Used
## Recording
### Video
|Name|Description|Reference|
|:---:|:---:|:---:|
|[SharpDX](http://sharpdx.org/)|Grabbing Screen from CPU|used in Desktop Duplication API, Windows.Graphics.Capture, uNvEncoder|
|Desktop Duplication API|Screen Capture ([#5](../../../issues/5))|[FScreen.cs](../Recording/Video/FScreen.cs)|
|[Windows.Graphics.Capture](https://github.com/Microsoft/Windows.UI.Composition-Win32-Samples/tree/master/dotnet/WPF/ScreenCapture)|Screen Capture ([#5](../../../issues/5))|[WGCHelper.cs](../Recording/Video/WGC/WGCHelper.cs) in [WGC Directory](../../../tree/main/Recording/Video/WGC)|
|[NvEncoder](https://github.com/NVIDIA/video-sdk-samples/blob/master/Samples/NvCodec/NvEncoder/NvEncoder.cpp)|Video Encoding with NVENC|[Lib.cs](../Recording/Video/NvEncoder/Lib.cs)|
|[uNvEncoder](https://github.com/hecomi/uNvEncoder)|NvEncoder for .NET|[Lib.cs](../Recording/Video/NvEncoder/Lib.cs), [Encoder.cs](../Recording/Video/NvEncoder/Encoder.cs)|
|[NvPipe](https://github.com/NVIDIA/NvPipe/blob/master/README.deprecated.md)|Video decoding with NVDNC|[Lib.cs](../Recording/Video/NvPipe/Lib.cs)|
|[uNvPipe](https://github.com/hecomi/uNvPipe)|NvPipe for .NET|[Lib.cs](../Recording/Video/NvPipe/Lib.cs), [Encoder.cs](../Recording/Video/NvPipe/Encoder.cs), [Decoder.cs (Not Used)](../Recording/Video/NvPipe/Decoder.cs)|
|[OpenH264Lib.NET](https://github.com/secile/OpenH264Lib.NET)|OpenH264 for .NET|[MainWindow.xaml.cs](../MainWindow.xaml.cs)|
|[OpenCvSharp](https://github.com/shimat/opencvsharp)|Color Converting ([#7](../../../issues/7))|.|
|[NvColorSpace](https://github.com/MineEric64/NvColorSpace)|Color Converting [#7](../../../issues/7))|.|
|[ProjectReinforced](https://github.com/Luigi38/ProjectReinforced)|Implementing Screen Capture|[FScreen.cs](../Recording/Video/FScreen.cs), [Rescreen.cs](../Recording/Video/Rescreen.cs)|

### Audio
|Name|Description|Reference|
|:---:|:---:|:---:|
|[win-capture-audio](https://github.com/bozbez/win-capture-audio)|Audio Capture from a specific application ([#3](../../../issues/3))|[AudioCaptureHelper.cs](../Recording/Audio/WinCaptureAudio/AudioCaptureHelper.cs) in [WinCaptureAudio Directory](../../../tree/main/Recording/Audio/WinCaptureAudio)|
|[AuxSense](https://github.com/SirusDoma/AuxSense)|**[Deprecated]** Hooking a specific application's audio instance ([#3](../../../issues/3))|[AudioRenderer.cs](../Recording/Audio/AudioRender/AudioRenderer.cs)|
|[PitchPitch](https://github.com/davinx/PitchPitch)|Implementing CoreAudioApi interfaces|[CoreAudioApi Directory](../../../tree/main/Recording/Audio/AudioRender/Types)|
|[NAudio](https://github.com/naudio/NAudio)|Capture & Play Audio|[WasapiCapture.cs](../Recording/Audio/Wasapi/WasapiCapture.cs), [WasapiPlay.cs](../Recording/Audio/Wasapi/WasapiPlay.cs)|
|[ProjectReinforced](https://github.com/Luigi38/ProjectReinforced)|Implementing Audio Capture & Mixer|[WasapiCapture.cs](../Recording/Audio/Wasapi/WasapiCapture.cs), [Mixer.cs](../Recording/Audio/WinCaptureAudio/Mixer.cs)|
|ProjectUnitor|Implementing Play Audio|[WasapiPlay.cs](../Recording/Audio/Wasapi/WasapiPlay.cs)|

## Clients
|Name|Description|Reference|
|:---:|:---:|:---:|
|[LiteNetLib](https://github.com/RevenantX/LiteNetLib)|Peer to Peer Communication ([#4](../../../issues/4))|[ClientOne.cs](../Clients/ClientOne.cs)|
|[MessagePack-CSharp](https://github.com/neuecc/MessagePack-CSharp)|Data Serializing|[ClientOne.cs](../Clients/ClientOne.cs), [Rescreen.cs](../Recording/Video/Rescreen.cs)|
|[Json.Net](https://www.newtonsoft.com/json)|Json Framework|[ClientOne.cs](../Clients//ClientOne.cs), [RoomManager.cs](../Rooms/RoomManager.cs)|
|[Discord Rich Presence](https://github.com/Lachee/discord-rpc-csharp)|Discord RPC|[DiscordHelper.cs](../Clients/DiscordHelper.cs)|
