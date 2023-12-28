using System;
using System.Collections.Generic;
using System.Text;

namespace RefactorParent.Test
{
    internal interface IMyStuff
    {
        void MyStuffMethod();
    }
    internal class MyStuff : IMyStuff
    {
        public void MyStuffMethod()
        {
            throw new NotImplementedException();
        }
    }
}
