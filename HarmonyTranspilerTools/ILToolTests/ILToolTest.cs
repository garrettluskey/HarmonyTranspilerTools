﻿using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;
using System.Reflection.Emit;
using Harmony;
using Harmony.ILCopying;
using System.Collections.Generic;
using System.Diagnostics;
using ILHelper;

namespace ILParserTests
{
    public class DummyCompiledClass
    {
        private int thing = 5;
        private int other = 6;

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
    }

    
    public class DummyModClass : DummyCompiledClass
    {
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

    }

    [HarmonyPatch(typeof(DummyCompiledClass))]
    [HarmonyPatch("SomeMethod")]
    public static class DummyHarmonyClass
    {
        public static IEnumerable<CodeInstruction> ReplaceMethodTranspiler(IEnumerable<CodeInstruction> instr)
        {
            MethodInfo info = typeof(DummyModClass).GetMethod("SomeMethod");
            List<CodeInstruction> newInstr = ILParser.MethodToILInstructions(info);
            return newInstr;
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
            List<CodeInstruction> helperInstructions = ILParser.MethodToILInstructions(methodToCompare);
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

            List<CodeInstruction> codeInstructions = ILParser.MethodToILInstructions(replacementMethod);

            ILParser parser = new ILParser(instructions);

            parser.ReplaceAllCodeBlocks(searchMethod, replacementMethod);

            return parser.instructions;
        }

        public static IEnumerable<CodeInstruction> ReplaceStatementsWithReturnTranspiler(IEnumerable<CodeInstruction> instr)
        {
            List<CodeInstruction> instructions = new List<CodeInstruction>(instr);

            MethodInfo searchMethod = typeof(DummyCompiledClass).GetMethod("SomeMethod");
            MethodInfo replacementMethod = typeof(DummyModClass).GetMethod("SomeMethod");

            ILParser parser = new ILParser(instructions);

            parser.ReplaceAllCodeBlocks(searchMethod, replacementMethod);

            return parser.instructions;
        }

        public static IEnumerable<CodeInstruction> AddStatementsBeforeTranspiler(IEnumerable<CodeInstruction> instr)
        {
            List<CodeInstruction> instructions = new List<CodeInstruction>(instr);

            MethodInfo searchMethod = typeof(DummyModClass).GetMethod("ForSearchStatements");
            MethodInfo replacementMethod = typeof(DummyModClass).GetMethod("ReplacementStatement");

            ILParser parser = new ILParser(instructions);

            parser.AddCodeBlockBefore(searchMethod, replacementMethod);

            return parser.instructions;
        }

        public static IEnumerable<CodeInstruction> AddStatementsAfterTranspiler(IEnumerable<CodeInstruction> instr)
        {
            List<CodeInstruction> instructions = new List<CodeInstruction>(instr);

            MethodInfo searchMethod = typeof(DummyModClass).GetMethod("ForSearchStatements");
            MethodInfo replacementMethod = typeof(DummyModClass).GetMethod("ReplacementStatement");

            ILParser parser = new ILParser(instructions);

            parser.AddCodeBlockAfter(searchMethod, replacementMethod);

            return parser.instructions;
        }

        public static IEnumerable<CodeInstruction> RemoveStatementsNoReturnTranspiler(IEnumerable<CodeInstruction> instr)
        {
            List<CodeInstruction> instructions = new List<CodeInstruction>(instr);

            MethodInfo removeMethod = typeof(DummyModClass).GetMethod("SearchStatement");

            ILParser parser = new ILParser(instructions);

            parser.RemoveCodeBlock(removeMethod);

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

        //[TestMethod]
        //public void PrintIL()
        //{
        //    MethodInfo thingMethod = typeof(DummyModClass).GetMethod("toFindStatement");
        //    MethodInfo complexMethod = typeof(DummyCompiledClass).GetMethod("ComplexMethod");
        //    Trace.WriteLine("--SomeMethod--");
        //    ILParser.MethodToILInstructions(thingMethod).Do(x => Trace.WriteLine(x));
        //    Trace.WriteLine("");
        //    Trace.WriteLine("--ComplexMethod--");
        //    ILParser.MethodToILInstructions(complexMethod).Do(x => Trace.WriteLine(x));
        //}

        
    }
}