# 사용한 라이브러리
## Recording
### Video
|이름|설명|참조|
|:---:|:---:|:---:|
|[SharpDX](http://sharpdx.org/)|CPU로부터 화면 가져오기|Desktop Duplication API, Windows.Graphics.Capture, uNvEncoder에서 사용됨|
|Desktop Duplication API|화면 캡쳐 ([#5](../../../issues/5))|[FScreen.cs](../Recording/Video/FScreen.cs)|
|[Windows.Graphics.Capture](https://github.com/Microsoft/Windows.UI.Composition-Win32-Samples/tree/master/dotnet/WPF/ScreenCapture)|화면 캡쳐 ([#5](../../../issues/5))|[WGC 폴더](../../../tree/main/Recording/Video/WGC)에 있는 [WGCHelper.cs](../Recording/Video/WGC/WGCHelper.cs)|
|[NvEncoder](https://github.com/NVIDIA/video-sdk-samples/blob/master/Samples/NvCodec/NvEncoder/NvEncoder.cpp)|NVENC를 이용한 영상 인코딩|[Lib.cs](../Recording/Video/NvEncoder/Lib.cs)|
|[uNvEncoder](https://github.com/hecomi/uNvEncoder)|.NET용 NvEncoder|[Lib.cs](../Recording/Video/NvEncoder/Lib.cs), [Encoder.cs](../Recording/Video/NvEncoder/Encoder.cs)|
|[NvPipe](https://github.com/NVIDIA/NvPipe/blob/master/README.deprecated.md)|NVDNC를 이용한 영상 디코딩|[Lib.cs](../Recording/Video/NvPipe/Lib.cs)|
|[uNvPipe](https://github.com/hecomi/uNvPipe)|.NET용 NvPipe|[Lib.cs](../Recording/Video/NvPipe/Lib.cs), [Encoder.cs](../Recording/Video/NvPipe/Encoder.cs), [Decoder.cs (사용되지 않음)](../Recording/Video/NvPipe/Decoder.cs)|
|[OpenH264Lib.NET](https://github.com/secile/OpenH264Lib.NET)|.NET용 OpenH264|[MainWindow.xaml.cs](../MainWindow.xaml.cs)|
|[OpenCvSharp](https://github.com/shimat/opencvsharp)|색상 변환 ([#7](../../../issues/7))|.|
|[NvColorSpace](https://github.com/MineEric64/NvColorSpace)|CUDA를 이용한 색상 변환 ([#7](../../../issues/7))|.|
|[ProjectReinforced](https://github.com/Luigi38/ProjectReinforced)|화면 캡쳐 구현|[FScreen.cs](../Recording/Video/FScreen.cs), [Rescreen.cs](../Recording/Video/Rescreen.cs)|

### Audio
|이름|설명|참조|
|:---:|:---:|:---:|
|[win-capture-audio](https://github.com/bozbez/win-capture-audio)|특정 프로그램으로부터 소리 캡쳐 ([#3](../../../issues/3))|[WinCaptureAudio 폴더](../../../tree/main/Recording/Audio/WinCaptureAudio)에 있는 [AudioCaptureHelper.cs](../Recording/Audio/WinCaptureAudio/AudioCaptureHelper.cs)|
|[AuxSense](https://github.com/SirusDoma/AuxSense)|**[사용되지 않음]** 특정 프로그램의 오디오 객체 후킹 ([#3](../../../issues/3))|[AudioRenderer.cs](../Recording/Audio/AudioRender/AudioRenderer.cs)|
|[PitchPitch](https://github.com/davinx/PitchPitch)|CoreAudioApi 인터페이스 구현|[CoreAudioApi 폴더](../../../tree/main/Recording/Audio/AudioRender/Types)|
|[NAudio](https://github.com/naudio/NAudio)|소리 캡쳐와 재생|[WasapiCapture.cs](../Recording/Audio/Wasapi/WasapiCapture.cs), [WasapiPlay.cs](../Recording/Audio/Wasapi/WasapiPlay.cs)|
|[ProjectReinforced](https://github.com/Luigi38/ProjectReinforced)|소리 캡처와 믹서 구현|[WasapiCapture.cs](../Recording/Audio/Wasapi/WasapiCapture.cs), [Mixer.cs](../Recording/Audio/WinCaptureAudio/Mixer.cs)|
|ProjectUnitor|소리 재생 구현|[WasapiPlay.cs](../Recording/Audio/Wasapi/WasapiPlay.cs)|

## Clients
|이름|설명|참조|
|:---:|:---:|:---:|
|[LiteNetLib](https://github.com/RevenantX/LiteNetLib)|P2P 연결 ([#4](../../../issues/4))|[ClientOne.cs](../Clients/ClientOne.cs)|
|[MessagePack-CSharp](https://github.com/neuecc/MessagePack-CSharp)|데이터 직렬화|[ClientOne.cs](../Clients/ClientOne.cs), [Rescreen.cs](../Recording/Video/Rescreen.cs)|
|[Json.Net](https://www.newtonsoft.com/json)|Json 프레임워크|[ClientOne.cs](../Clients//ClientOne.cs), [RoomManager.cs](../Rooms/RoomManager.cs)|
|[Discord Rich Presence](https://github.com/Lachee/discord-rpc-csharp)|디스코드 RPC|[DiscordHelper.cs](../Clients/DiscordHelper.cs)|
