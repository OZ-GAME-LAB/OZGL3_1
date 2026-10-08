using System.Runtime.CompilerServices;

// UI 구현 어셈블리만 GameUI 등록/이벤트 발생 같은 내부 기능에 접근할 수 있게 한다.
[assembly: InternalsVisibleTo("OZ.UI")]
[assembly: InternalsVisibleTo("OZ.UI.Editor")]
