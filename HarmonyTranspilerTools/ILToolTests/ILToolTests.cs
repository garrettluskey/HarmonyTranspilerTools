using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Harmony.ILCopying;
using System.Collections.Generic;
using System.Diagnostics;
using HarmonyTranspilerTools;

namespace ILToolsTests
{
    public class DummyCompiledClass
    {
        private int thing = 5;
        private int other = 6;
        public int testVar;

        public bool SomeMethod()
        {
            // Replace with true
            return false;
        }

        public int ComplexMethod(int y)
        {
            for (int i = 0; i < 10; i++)
            {
                y++;
            }
            return y;
        }

        public void SetPrivateInstanceVariable()
        {
            thing = 1;
        }

        public void SetPublicInstanceVariable()
        {
            testVar = 10;
        }
    }

    
    public class DummyModClass : DummyCompiledClass
    {
        private int thing = 5;
        public void SearchStatement(int y)
        {
            y++;
        }

        public void ReplacementStatement(int y)
        {
            y = y + 2;
        }

        public void ForSearchStatements(int y)
        {
            for (int i = 0; i < 10; i++)
            {
                y++;
            }
        }

        public new bool SomeMethod()
        {
            return true;
        }

        public void SetPrivateInstanceVariable()
        {
            thing = 2;
        }

        public void SetPublicInstanceVariable()
        {
            testVar = 20;
        }

    }

    [HarmonyPatch(typeof(DummyCompiledClass))]
    [HarmonyPatch("SomeMethod")]
    public static class DummyHarmonyClass
    {
        public static IEnumerable<CodeInstruction> ReplaceMethodTranspiler(IEnumerable<CodeInstruction> instr)
        {
            MethodInfo info = typeof(DummyModClass).GetMethod("SomeMethod");

            ILTool tool = new ILTool(instr);
            tool.ReplaceMethod(info);

            return tool.instructions;
        }

        /// <summary>
        /// Used strictly and only used in CompileIL test.
        /// This method compares the IL code of Harmony's transpiler and ILHelpers transpiler
        /// </summary>
        /// <param name="instr">Harmony's transpiled IL code</param>
        /// <returns>Instructions to replace patch statement</returns>
        public static IEnumerable<CodeInstruction> ILComparison(IEnumerable<CodeInstruction> instr)
        {
            // Complex method is used 
            MethodInfo methodToCompare = typeof(DummyCompiledClass).GetMethod("ComplexMethod");
            List<CodeInstruction> helperInstructions = ILTool.MethodToILInstructions(methodToCompare);
            List<CodeInstruction> harmonyInstructions = (List<CodeInstruction>)instr;

            // Verify helper and harmony instructions are equal
            Assert.AreEqual(harmonyInstructions.Count, helperInstructions.Count);

            for (int i = 0; i < helperInstructions.Count; i++)
            {
                // Check opcodes are equal
                Assert.AreEqual(harmonyInstructions[i].opcode, helperInstructions[i].opcode);
                // Check operands are equal
                Assert.AreEqual(harmonyInstructions[i].operand, helperInstructions[i].operand);

                // Check exception blocks are equal
                Assert.AreEqual(harmonyInstructions[i].blocks.Count, helperInstructions[i].blocks.Count);

                for (int j = 0; j < harmonyInstructions[i].blocks.Count; j++)
                {
                    Assert.AreEqual(harmonyInstructions[i].blocks[j], helperInstructions[i].blocks[j]);
                }

                // Check labels are equal
                Assert.AreEqual(harmonyInstructions[i].labels.Count, helperInstructions[i].labels.Count);

                for (int j = 0; j < harmonyInstructions[i].labels.Count; j++)
                {
                    Assert.AreEqual(harmonyInstructions[i].labels[j], helperInstructions[i].labels[j]);
                }
            }

            return instr;
        }

        public static IEnumerable<CodeInstruction> ReplaceStatementsNoReturnTranspiler(IEnumerable<CodeInstruction> instr)
        {
            List<CodeInstruction> instructions = new List<CodeInstruction>(instr);

            MethodInfo searchMethod = typeof(DummyModClass).GetMethod("SearchStatement");
            MethodInfo replacementMethod = typeof(DummyModClass).GetMethod("ReplacementStatement");

            ILTool parser = new ILTool(instructions);

            parser.ReplaceAllCodeBlocks(searchMethod, replacementMethod);

            return parser.instructions;
        }

        public static IEnumerable<CodeInstruction> ReplaceStatementsWithReturnTranspiler(IEnumerable<CodeInstruction> instr)
        {
            List<CodeInstruction> instructions = new List<CodeInstruction>(instr);

            MethodInfo searchMethod = typeof(DummyCompiledClass).GetMethod("SomeMethod");
            MethodInfo replacementMethod = typeof(DummyModClass).GetMethod("SomeMethod");

            ILTool parser = new ILTool(instructions);

            parser.ReplaceAllCodeBlocks(searchMethod, replacementMethod);

            return parser.instructions;
        }

        public static IEnumerable<CodeInstruction> AddStatementsBeforeTranspiler(IEnumerable<CodeInstruction> instr)
        {
            List<CodeInstruction> instructions = new List<CodeInstruction>(instr);

            MethodInfo searchMethod = typeof(DummyModClass).GetMethod("ForSearchStatements");
            MethodInfo replacementMethod = typeof(DummyModClass).GetMethod("ReplacementStatement");

            ILTool parser = new ILTool(instructions);

            parser.AddCodeBlockBefore(searchMethod, replacementMethod);

            return parser.instructions;
        }

        public static IEnumerable<CodeInstruction> AddStatementsAfterTranspiler(IEnumerable<CodeInstruction> instr)
        {
            List<CodeInstruction> instructions = new List<CodeInstruction>(instr);

            MethodInfo searchMethod = typeof(DummyModClass).GetMethod("ForSearchStatements");
            MethodInfo replacementMethod = typeof(DummyModClass).GetMethod("ReplacementStatement");

            ILTool parser = new ILTool(instructions);

            parser.AddCodeBlockAfter(searchMethod, replacementMethod);

            return parser.instructions;
        }

        public static IEnumerable<CodeInstruction> RemoveStatementsNoReturnTranspiler(IEnumerable<CodeInstruction> instr)
        {
            List<CodeInstruction> instructions = new List<CodeInstruction>(instr);

            MethodInfo removeMethod = typeof(DummyModClass).GetMethod("SearchStatement");

            ILTool parser = new ILTool(instructions);

            parser.RemoveCodeBlock(removeMethod);

            return parser.instructions;
        }

        public static IEnumerable<CodeInstruction> SetPublicInstanceVariableTranspiler(IEnumerable<CodeInstruction> instr)
        {
            MethodInfo info = typeof(DummyModClass).GetMethod("SetPublicInstanceVariable");
            List<CodeInstruction> newInstr = ILTool.MethodToILInstructions(info);
            return newInstr;
        }

        public static IEnumerable<CodeInstruction> SetPrivateInstanceVariableTranspiler(IEnumerable<CodeInstruction> instr)
        {
            MethodInfo info = typeof(DummyModClass).GetMethod("SetPrivateInstanceVariable");
            ILTool parser = new ILTool(instr);
            parser.ReplaceMethod(info);
            return parser.instructions;
        }
    }

    [TestClass]
    public class ILGenerationTest
    {
        public static HarmonyInstance harmony = HarmonyInstance.Create("testing.ILGenerationTest");

        [TestCleanup]
        public void Cleanup()
        {
            harmony.UnpatchAll(harmony.Id);
        }

        [TestMethod]
        public void SetValueDirect()
        {
            DummyCompiledClass madeClass = new DummyCompiledClass();
            DummyModClass mc = new DummyModClass();

            TypedReference reference = __makeref(mc);

            FieldInfo info = madeClass.GetType().GetField("thing", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.AreEqual(5, (int)info.GetValueDirect(reference));

            info.SetValueDirect(reference, 9);
            Assert.AreEqual(9, (int)info.GetValueDirect(reference));
        }

        /// <summary>
        /// Verifies that helper IL compiler is equivilent to harmony IL compiler
        /// </summary>
        [TestMethod]
        public void HelperCompileIL()
        {
            HarmonyInstance harmony = HarmonyInstance.Create("testing.CompileIL");
            MethodInfo methodToReplace = typeof(DummyCompiledClass).GetMethod("ComplexMethod");
            MethodInfo transpiler = typeof(DummyHarmonyClass).GetMethod("ILComparison");
            harmony.Patch(methodToReplace, transpiler: new HarmonyMethod(transpiler));
        }

        

        [TestMethod]
        public void ReplaceEntireMethod()
        {
            DummyCompiledClass compiledClass = new DummyCompiledClass();

            MethodInfo methodToReplace = typeof(DummyCompiledClass).GetMethod("SomeMethod");
            MethodInfo transpiler = typeof(DummyHarmonyClass).GetMethod("ReplaceMethodTranspiler");

            Assert.IsFalse(compiledClass.SomeMethod());

            harmony.Patch(methodToReplace, transpiler: new HarmonyMethod(transpiler));

            Assert.IsTrue(compiledClass.SomeMethod());
        }

        [TestMethod]
        public void ReplaceStatementsNoReturn()
        {
            int yValue = 0;

            DummyCompiledClass compiledClass = new DummyCompiledClass();

            MethodInfo methodToReplace = typeof(DummyCompiledClass).GetMethod("ComplexMethod");
            MethodInfo transpiler = typeof(DummyHarmonyClass).GetMethod("ReplaceStatementsNoReturnTranspiler");

            Assert.AreEqual(10, compiledClass.ComplexMethod(yValue));

            harmony.Patch(methodToReplace, transpiler: new HarmonyMethod(transpiler));

            Assert.AreEqual(20, compiledClass.ComplexMethod(yValue));
        }

        [TestMethod]
        public void ReplaceStatementsWithReturn()
        {
            DummyCompiledClass compiledClass = new DummyCompiledClass();

            MethodInfo methodToReplace = typeof(DummyCompiledClass).GetMethod("SomeMethod");
            MethodInfo transpiler = typeof(DummyHarmonyClass).GetMethod("ReplaceStatementsWithReturnTranspiler");

            Assert.IsFalse(compiledClass.SomeMethod());

            harmony.Patch(methodToReplace, transpiler: new HarmonyMethod(transpiler));

            Assert.IsTrue(compiledClass.SomeMethod());
        }

        [TestMethod]
        public void AddStatementsBefore()
        {
            int yValue = 0;

            DummyCompiledClass compiledClass = new DummyCompiledClass();

            MethodInfo methodToReplace = typeof(DummyCompiledClass).GetMethod("ComplexMethod");
            MethodInfo transpiler = typeof(DummyHarmonyClass).GetMethod("AddStatementsBeforeTranspiler");

            Assert.AreEqual(10, compiledClass.ComplexMethod(yValue));

            harmony.Patch(methodToReplace, transpiler: new HarmonyMethod(transpiler));

            Assert.AreEqual(12, compiledClass.ComplexMethod(yValue));
        }

        [TestMethod]
        public void AddStatementsAfter()
        {
            int yValue = 0;

            DummyCompiledClass compiledClass = new DummyCompiledClass();

            MethodInfo methodToReplace = typeof(DummyCompiledClass).GetMethod("ComplexMethod");
            MethodInfo transpiler = typeof(DummyHarmonyClass).GetMethod("AddStatementsAfterTranspiler");

            Assert.AreEqual(10, compiledClass.ComplexMethod(yValue));

            harmony.Patch(methodToReplace, transpiler: new HarmonyMethod(transpiler));

            Assert.AreEqual(12, compiledClass.ComplexMethod(yValue));
        }

        [TestMethod]
        public void RemoveStatements()
        {
            int yValue = 0;

            DummyCompiledClass compiledClass = new DummyCompiledClass();

            MethodInfo methodToReplace = typeof(DummyCompiledClass).GetMethod("ComplexMethod");
            MethodInfo transpiler = typeof(DummyHarmonyClass).GetMethod("RemoveStatementsNoReturnTranspiler");

            Assert.AreEqual(10, compiledClass.ComplexMethod(yValue));

            harmony.Patch(methodToReplace, transpiler: new HarmonyMethod(transpiler));

            Assert.AreEqual(yValue, compiledClass.ComplexMethod(yValue));
        }

        [TestMethod]
        public void SetPublicInstanceVariableTest()
        {
            DummyCompiledClass compiledClass = new DummyCompiledClass();

            MethodInfo methodToReplace = typeof(DummyCompiledClass).GetMethod("SetPublicInstanceVariable");
            MethodInfo transpiler = typeof(DummyHarmonyClass).GetMethod("SetPublicInstanceVariableTranspiler");

            compiledClass.SetPublicInstanceVariable();
            Assert.AreEqual(10, compiledClass.testVar);

            harmony.Patch(methodToReplace, transpiler: new HarmonyMethod(transpiler));

            compiledClass.SetPublicInstanceVariable();
            Assert.AreEqual(20, compiledClass.testVar);
        }

        [TestMethod]
        public void SetPrivateInstanceVariableTest()
        {
            DummyCompiledClass compiledClass = new DummyCompiledClass();

            MethodInfo methodToReplace = typeof(DummyCompiledClass).GetMethod("SetPrivateInstanceVariable");
            MethodInfo transpiler = typeof(DummyHarmonyClass).GetMethod("SetPrivateInstanceVariableTranspiler");

            int privateVar;
            compiledClass.SetPrivateInstanceVariable();
            privateVar = (int)compiledClass.GetType().GetField("thing", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(compiledClass);
            Assert.AreEqual(1, privateVar);

            harmony.Patch(methodToReplace, transpiler: new HarmonyMethod(transpiler));

            compiledClass.SetPrivateInstanceVariable();
            privateVar = (int)compiledClass.GetType().GetField("thing", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(compiledClass);
            Assert.AreEqual(2, privateVar);
        }


        //[TestMethod]
        //public void PrintIL()
        //{
        //    MethodInfo thingMethod = typeof(DummyModClass).GetMethod("toFindStatement");
        //    MethodInfo complexMethod = typeof(DummyCompiledClass).GetMethod("ComplexMethod");
        //    Trace.WriteLine("--SomeMethod--");
        //    ILTool.MethodToILInstructions(thingMethod).Do(x => Trace.WriteLine(x));
        //    Trace.WriteLine("");
        //    Trace.WriteLine("--ComplexMethod--");
        //    ILTool.MethodToILInstructions(complexMethod).Do(x => Trace.WriteLine(x));
        //}


    }
}
