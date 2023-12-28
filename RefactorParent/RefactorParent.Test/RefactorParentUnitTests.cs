using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.IO;
using System.Threading.Tasks;
using VerifyCS = RefactorParent.Test.CSharpCodeFixVerifier<
    RefactorParent.RefactorParentAnalyzer,
    RefactorParent.RefactorParentCodeFixProvider>;

namespace RefactorParent.Test
{
    [TestClass]
    public class RefactorParentUnitTest : ITestInterface
    {
        //No diagnostics expected to show up
        [TestMethod]
        public async Task TestMethod1()
        {
            var test = @"";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public async Task TestMethod3()
        {
            var sourceCode = File.ReadAllText("G:\\Repos\\RefactorParent\\RefactorParent\\RefactorParent.Test\\MyStuff.cs");

            await VerifyCS.VerifyAnalyzerAsync(sourceCode);

            var expected = VerifyCS.Diagnostic("RefactorParent");

            await VerifyCS.VerifyCodeFixAsync(sourceCode, expected, string.Empty);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public async Task TestMethod2()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class {|#0:TypeName|}
        {   
        }
    }";

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class TYPENAME
        {   
        }
    }";

            var expected = VerifyCS.Diagnostic("RefactorParent").WithLocation(0).WithArguments("TypeName");
            await VerifyCS.VerifyCodeFixAsync(test, expected, fixtest);
        }

        public void MyTestMethod(string s)
        {
            throw new NotImplementedException();
        }
    }
    internal interface ITestInterface
    {
        void MyTestMethod(string s);
    }

}
