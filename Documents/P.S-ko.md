# 개발 과정
## 화면 캡처
### Desktop Duplication API
이 API를 쓸 때 제가 만든 프로젝트인 [ProjectReinforced](https://github.com/Luigi38/ProjectReinforced)에서 많은 도움이 되었습니다.
또한 쓰면서 DXGI, Direct3D11에 대한 많은 기능과 쓰는 이유를 그나마 알게 된 것 같습니다.
그리고 이 API도 충분히 빠르지만 더 빠르게 만들기 위해 [캡처한 화면을 가져오면서 화면 해상도를 줄여](https://stackoverflow.com/questions/44325283/custom-output-resolution-duplicateoutput-dxgi) 품질은 희생했지만 속도를 가져오게 되었습니다.
그리고 가져온 화면을 Buffer에 복사하기 위해 for문으로 height번을 반복해야 했지만,
병렬 프로그래밍을 통해 for문을 병렬 for로 교체했습니다.

하지만 인게임 테스트를 통하여 그냥 for문이나 병렬 for 성능 향상은 크게 없는걸로 보여 다시 기존 for문으로 교체했습니다.

## 소리 캡처
### AuxSense (오디오 후킹)
후킹을 시도하면서 특정 프로그램이나 DLL의 주소를 알게 되면 그 주소를 가로채 제가 만든 함수를 특정 프로그램이나 DLL 내에서 쓸 수 있다는 것을 알게 되었습니다. 제가 첫 후킹을 시도하고 성공하게 되었을 때 엄청 신기했습니다.
하지만 non-init 이슈를 통해 완벽하게 오디오를 캡처할 수 없어 결국 win-capture-audio로 넘어오게 되었습니다.

그래도 AuxSense를 만지게 되면서 win-capture-audio를 C#으로 구현할 때 많은 도움이 되었습니다.

### win-capture-audio
사실 특정 프로그램 소리를 캡처할 때 obs에서는 [win-capture-audio](https://github.com/bozbez/win-capture-audio)라는 플러그인도 쓴다는 것을 처음에 구글링할 때 알게 되었습니다.
하지만 특정 프로그램 소리 캡처 라이브러리 그런 것은 존재하지 않아 제가 직접 c++ 플러그인을 c#으로 포팅하고 제 프로젝트에 맞게 코드를 짜게 되었습니다.

## 인코딩/디코딩
### NVENC
원래는 [MessagePack-CSharp](https://github.com/neuecc/MessagePack-CSharp) 라이브러리를 통해 Raw Buffer를 압축하여 전송했지만, 그렇게 해도 데이터 크기가 엄청 커서 전송이 좀 느렸었습니다.
따라서 인코딩 라이브러리를 검색해보니 Nvidia 그래픽카드에서 지원하는 NVENC를 쓸 수 있다는 것을 알게 되었습니다.
결국 검색을 통해 NvEncoder와 NvPipe 라이브러리를 사용하여 효율적으로 인코딩할 수 있게 되었습니다.

인게임 테스트에서 하드웨어 인코딩 라이브러리 없이 캡처하면 1280x720 해상도로 캡처해도 7fps밖에 안되지만,
NVENC를 사용할 경우 2560x1440 해상도로 캡처해도 70fps 넘게 캡처할 수 있었습니다.

현재는 디코딩 라이브러리가 색상을 RGBA 밖에 지원이 되지 않아서 BGRA도 지원을 할 수 있게 제가 직접 디코딩 라이브러리를 포팅 중입니다.

# 여담
이 프로젝트를 만들면서 핵심 기능 (화면/소리 캡처, 인코딩/디코딩)을 만들 때는 하루에 평균 12시간 코딩하면서 새로운 기능 구현 및 버그를 잡으면서 생활했습니다.
이 모든 핵심 기능이 2주만에 만들어졌습니다.
제가 생각하기엔 이 프로젝트 핵심 기능만 구현하는데 100시간 넘게 걸린 것 같습니다.

[UniConverter](https://github.com/MineEric64/UniConverter-Project), [SendAnywhere API](https://github.com/MineEric64/SendAnywhere-py)와 [ProjectReinforced](https://github.com/Luigi38/ProjectReinforced) 프로젝트 다음으로 만들기 제일 힘들고 어려웠던 것 같습니다.
특히 저는 C++도 사용하지만 Windows API를 쓰면서 C++을 쓰니 디버깅하기도 어렵고 문법도 어려워 버그 잡기가 다른 프로젝트에 비해 더욱 어려웠던 것 같습니다.
제가 하는 프로젝트들은 구글링을 열심히 해도 많이 안나오니 새로운 기능 만들 때도 버그를 잡을 때도 되게 힘들더군요.
물론 갓-Stack Overflow에서 가끔씩 나오긴 합니다. 진짜 Stack Overflow에는 없는게 없는 것 같네요.

이 프로젝트를 사용해주시는 여러분께 감사드립니다.
[어느 버그 리포트나 Issue](https://github.com/MineEric64/BetterLiveScreen/issues)는 환영입니다!