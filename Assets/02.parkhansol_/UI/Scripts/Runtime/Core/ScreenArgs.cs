using System.Collections.Generic;
using OZ.UI.Contracts;

namespace OZ.UI
{
    // UIManager.Open(id, args)로 창에 넘기는 데이터. 창은 OnSetup(object args)에서 형변환해 사용.

    public sealed class TitleArgs
    {
        public readonly bool CanContinue;
        public TitleArgs(bool canContinue) { CanContinue = canContinue; }
    }

    public sealed class ClassSelectArgs
    {
        public readonly IReadOnlyList<ClassData> Classes;
        public ClassSelectArgs(IReadOnlyList<ClassData> classes) { Classes = classes; }
    }

}
