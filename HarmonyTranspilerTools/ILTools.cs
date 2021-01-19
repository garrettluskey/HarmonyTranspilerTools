using System;
using System.Collections.Generic;
using HarmonyLib;
using System.Reflection.Emit;
using System.Reflection;
using System.Diagnostics;

namespace HarmonyTranspilerTools
{
	public class Range
	{
		public int start;
		public int end;

		public Range(int start, int end)
		{
			this.start = start;
			this.end = end;
		}

		public override bool Equals(object value)
		{
			Range val = (Range)value;
			return val.start == start && val.end == end;
		}

        public override int GetHashCode()
        {
            int hashCode = 1075529825;
            hashCode = hashCode * -1521134295 + start.GetHashCode();
            hashCode = hashCode * -1521134295 + end.GetHashCode();
            return hashCode;
        }

        public override string ToString()
		{
			return start.ToString() + ", " + end.ToString();
		}
	}

    public class ILTool
    {
		private static readonly Assembly a_Harmony = Assembly.GetAssembly(typeof(Harmony));

		private static Type t_MethodBodyReader = a_Harmony.GetType("HarmonyLib.MethodBodyReader");
		private static Type t_Emitter = a_Harmony.GetType("HarmonyLib.Emitter");

		/// <summary>
		/// Compiles a method into IL instructions exactly the same as the Harmony transpiler.
		/// </summary>
		/// <param name="method">Method to compile.</param>
		/// <returns>
		/// List of ILInstructions.
		/// </returns>
		/// <remarks>
		/// Utilizes harmony library.
		/// </remarks>
		public static List<CodeInstruction> MethodToILInstructions(MethodBase method)
		{
            // Get il generator
            ILGenerator il_gen = PatchProcessor.CreateILGenerator(method);

            List<Label> labels = new List<Label>();
			List<MethodInfo> transpilers = new List<MethodInfo>();
			bool hasReturn = false;

			// internal Emitter(ILGenerator il, bool debug)
			object emitter = Activator.CreateInstance(
				t_Emitter,
				BindingFlags.NonPublic | BindingFlags.Instance,
				null,
				new object[] { il_gen, true },
				null);

			// internal MethodBodyReader(MethodBase method, ILGenerator generator)
			object methodBodyReader = Activator.CreateInstance(
				t_MethodBodyReader,
				BindingFlags.NonPublic | BindingFlags.Instance,
				null,
				new object[] { method, il_gen },
				null);

			// internal void DeclareVariables(LocalBuilder[] existingVariables)
			t_MethodBodyReader.GetMethod("DeclareVariables", BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(methodBodyReader, new object[] { null });

			// internal void ReadInstructions()
			t_MethodBodyReader.GetMethod("ReadInstructions", BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(methodBodyReader, new object[0]);

			// internal List<CodeInstruction> FinalizeILCodes(Emitter emitter, List<MethodInfo> transpilers, List<Label> endLabels, out bool hasReturnCode)
			List<CodeInstruction> generated_instructions = (List<CodeInstruction>)t_MethodBodyReader
				.GetMethod("FinalizeILCodes", BindingFlags.NonPublic | BindingFlags.Instance)
				.Invoke(methodBodyReader, new object[] { emitter, transpilers, labels, hasReturn });

			// Add final return statement as harmony adds it manually after???
			generated_instructions.Add(new CodeInstruction(OpCodes.Ret, null));

			return generated_instructions;
		}


        private List<CodeInstruction> _instructions = new List<CodeInstruction>();

		public List<CodeInstruction> instructions
		{
			get { return _instructions; }
			private set
			{
				_instructions = ReplaceVarsWithSameName(value);
			}
		}

		

		public List<FieldInfo> variables = new List<FieldInfo>();
		public ILTool(IEnumerable<CodeInstruction> instr)
		{
			List<CodeInstruction> instructions = new List<CodeInstruction>(instr);
			foreach (CodeInstruction instruction in instructions)
			{
				if (instruction.operand != null && instruction.operand is FieldInfo)
				{
					variables.Add((FieldInfo)instruction.operand);
				}
			}
			this.instructions = instructions;
		}


		/// <summary>
		/// Replace declared variables with compiled variables
		/// </summary>
		/// <param name="instr"></param>
		/// <returns></returns>
		private List<CodeInstruction> ReplaceVarsWithSameName(List<CodeInstruction> instructions)
		{
			// Replace declared variables with compiled variables
			for (int i = 0; i < instructions.Count; i++)
			{
				if (instructions[i].operand != null && instructions[i].operand is FieldInfo)
				{
					FieldInfo operand = (FieldInfo)instructions[i].operand;
					variables.Do(x =>
					{
						if (x.Name == operand.Name)
						{
							instructions[i].operand = x;
						}
					});
				}
			}

			return instructions;
		}


        /// <summary>
        /// Replaces an entire function with another
        /// </summary>
        /// <param name="method">Method used for replacement</param>
        public void ReplaceMethod(MethodInfo method)
        {
            instructions = MethodToILInstructions(method);
        }

        /// <summary>
        /// Places additional CodeBlock statements after all instances of codeBlockToFind statements.
        /// </summary>
        /// <param name="codeBlockToFind">Code block to search for in instructions list</param>
        /// <param name="additionCodeBlock">Code block to add in instructions list</param>
        /// <remarks>This method removes entry nop and void return from additionCodeBlock statements</remarks>
        public void AddCodeBlockAfter(MethodInfo codeBlockToFind, MethodInfo additionCodeBlock)
        {
            List<CodeInstruction> instructionsToFind = MethodToILInstructions(codeBlockToFind);
            List<CodeInstruction> instructionAddition = MethodToILInstructions(additionCodeBlock);

            instructionsToFind = MethodCleaner.RemoveEntryAndReturn(instructionsToFind);
            instructionAddition = MethodCleaner.RemoveEntryAndReturn(instructionAddition);

            instructionAddition.AddRange(instructionsToFind);

            ReplaceInstancesInList(instructionsToFind, instructionAddition);
        }

        /// <summary>
        /// Places additional CodeBlock statements before all instances of codeBlockToFind statements.
        /// </summary>
        /// <param name="codeBlockToFind">Code block to search for in instructions list</param>
        /// <param name="additionCodeBlock">Code block to add in instructions list</param>
        /// <remarks>This method removes entry nop and void return from additionCodeBlock statements</remarks>
        public void AddCodeBlockBefore(MethodInfo codeBlockToFind, MethodInfo additionCodeBlock)
        {
            List<CodeInstruction> instructionsToFind = MethodToILInstructions(codeBlockToFind);
            List<CodeInstruction> instructionAddition = MethodToILInstructions(additionCodeBlock);

            instructionsToFind = MethodCleaner.RemoveEntryAndReturn(instructionsToFind);
            instructionAddition = MethodCleaner.RemoveEntryAndReturn(instructionAddition);

            instructionAddition.InsertRange(0, instructionsToFind);

            ReplaceInstancesInList(instructionsToFind, instructionAddition);
        }

        /// <summary>
        /// Replaces all instances of codeBlockToFind with replacementCodeBlock.
        /// </summary>
        /// <param name="codeBlockToFind"></param>
        /// <param name="replacementCodeBlock"></param>
        public void ReplaceAllCodeBlocks(MethodInfo codeBlockToFind, MethodInfo replacementCodeBlock)
        {
            List<CodeInstruction> instructionsToFind = MethodToILInstructions(codeBlockToFind);
            List<CodeInstruction> instructionReplacement = MethodToILInstructions(replacementCodeBlock);

            instructionsToFind = MethodCleaner.RemoveEntryAndReturn(instructionsToFind);
            instructionReplacement = MethodCleaner.RemoveEntryAndReturn(instructionReplacement);

            ReplaceInstancesInList(instructionsToFind, instructionReplacement);

        }

        /// <summary>
        /// Remove all instances of codeBlockToFind from instructions.
        /// </summary>
        /// <param name="codeBlockToFind">Code block to remove for in instructions list</param>
        /// <remarks>This method removes entry nop and void return from codeBlockToFind statements</remarks>
        public void RemoveCodeBlock(MethodInfo codeBlockToRemove)
        {
            List<CodeInstruction> instructionsToFind = MethodToILInstructions(codeBlockToRemove);

            instructionsToFind = MethodCleaner.RemoveEntryAndReturn(instructionsToFind);

            ReplaceInstancesInList(instructionsToFind, new List<CodeInstruction>());
        }

        /// <summary>
        /// Finds all instances in code instruction list and replaces them with the given replacement
        /// </summary>
        /// <param name="query">Statement to find and replace</param>
        /// <param name="replacement">Statement use to replace queried instances</param>
        private void ReplaceInstancesInList(List<CodeInstruction> query, List<CodeInstruction> replacement)
		{
			List<CodeInstruction> finalList = new List<CodeInstruction>();
			for (int i = 0; i < instructions.Count; i++)
			{
				if (IsNextElements(i, instructions, query))
				{
					i += (query.Count - 1);
					finalList.InsertRange(finalList.Count, replacement);
				}
				else
				{
					finalList.Add(instructions[i]);
				}
			}
			instructions = finalList;
		}

		/// <summary>
		/// Checks if query is at posistion i to position j
		/// </summary>
		/// <param name="i">Current iterator location</param>
		/// <param name="arr">Current search array</param>
		/// <param name="query">Statement to match</param>
		/// <returns>Returns true if the elements at position i to position query length equal query otherwise false</returns>
		private bool IsNextElements(int i, List<CodeInstruction> arr, List<CodeInstruction> query)
		{
			for (int j = 0; j < query.Count; j++)
			{
				if (!CodeInstructionsEqual(arr[j + i], query[j]))
					return false;
			}
			return true;
		}

		/// <summary>
		/// Compares values of two code instructions
		/// </summary>
		/// <param name="instruction1">First instruction to compare</param>
		/// <param name="instruction2">Second instruction to compare</param>
		/// <returns>Returns true if the instructions are equal otherwise false</returns>
		public static bool CodeInstructionsEqual(CodeInstruction compiledInstruction, CodeInstruction replacementInstruction)
		{
			if (compiledInstruction.opcode != replacementInstruction.opcode)
			{
				return false;
			}

			if (compiledInstruction.operand != replacementInstruction.operand && 
				!compiledInstruction.operand.Equals(replacementInstruction.operand))
			{
				return false;
			}
				

			if (compiledInstruction.blocks.Count != replacementInstruction.blocks.Count)
				return false;

			if (compiledInstruction.labels.Count != replacementInstruction.labels.Count)
				return false;

			for (int i = 0; i < compiledInstruction.labels.Count; i++)
			{
				if (compiledInstruction.labels[i] != replacementInstruction.labels[i])
					return false;
			}

			for (int i = 0; i < compiledInstruction.blocks.Count; i++)
			{
				if (!compiledInstruction.blocks[i].Equals(replacementInstruction.blocks[i]))
					return false;
			}

			return true;

		}		
	}

	public static class MethodCleaner
	{
		//public static readonly List<OpCode> loadArgCodes = new List<OpCode>()
		//{
		//	OpCodes.Ldarg,
		//	OpCodes.Ldarga,
		//	OpCodes.Ldarg_0,
		//	OpCodes.Ldarg_1,
		//	OpCodes.Ldarg_2,
		//	OpCodes.Ldarg_3,
		//	OpCodes.Ldarg_S,
		//};

		public static readonly List<OpCode> loadLocalCodes = new List<OpCode>()
		{
			OpCodes.Ldloc,
			OpCodes.Ldloca,
			OpCodes.Ldloc_0,
			OpCodes.Ldloc_1,
			OpCodes.Ldloc_2,
			OpCodes.Ldloc_3,
			OpCodes.Ldloc_S,
		};

		//public static readonly List<OpCode> storeLocalCodes = new List<OpCode>()
		//{
		//	OpCodes.Stloc,
		//	OpCodes.Stloc_0,
		//	OpCodes.Stloc_1,
		//	OpCodes.Stloc_2,
		//	OpCodes.Stloc_3,
		//	OpCodes.Stloc_S,
		//};
		/// <summary>
		/// 
		/// </summary>
		/// <param name="instructions"></param>
		/// <returns></returns>
		public static List<CodeInstruction> RemoveEntryAndReturn(List<CodeInstruction> instructions)
		{
			instructions = RemoveEntry(instructions);
			instructions = RemoveReturn(instructions);
			return instructions;
		}

		/// <summary>
		/// Removes entry statement from IL code
		/// </summary>
		/// <param name="instructions">intructions to remove entry statement</param>
		/// <returns>Returns bool if the statement was removed or not</returns>
		/// <remarks>Every function starts with a nop statement. This method removes that statement</remarks>
		public static List<CodeInstruction> RemoveEntry(List<CodeInstruction> instructions)
		{
			if (instructions[0].opcode == OpCodes.Nop)
			{
				instructions.RemoveAt(0);

			}
			else
			{
				throw new SystemException("Entry statement not found");
			}
			return instructions;
		}

		/// <summary>
		/// Removes return statement from IL code
		/// </summary>
		/// <param name="instructions">intructions to remove return statement</param>
		/// <returns>Returns List<CodeInstruction> </returns>
		/// <remarks>Only use for void statements</remarks>
		public static List<CodeInstruction> RemoveReturn(List<CodeInstruction> instructions)
		{
			int instLen = instructions.Count;
			// Do not remove return if method returns something other than null
			if (instructions[instLen - 3].opcode == OpCodes.Br &&
				loadLocalCodes.Contains(instructions[instLen - 2].opcode) &&
				instructions[instLen - 1].opcode == OpCodes.Ret)
			{
				return instructions;
			}
			else if(instructions[instLen - 1].opcode == OpCodes.Ret)
			{
				instructions.RemoveAt(instLen - 1);
			}
			else
			{
				throw new SystemException("Return statement not found");
			}
			return instructions;
		}

		//public static List<CodeInstruction> RemoveParameterInits(MethodInfo queryMethod, List<CodeInstruction> instructions)
		//{
		//	int removeCounter = 0;
		//	int paramCount = queryMethod.GetParameters().Length;
		//	Trace.WriteLine(paramCount);
		//	for (int i = 0; i < instructions.Count - 1 && removeCounter < paramCount; i++)
		//	{
		//		// IL loads arg and stores as local
		//		// ex.
		//		// 
		//		if(loadArgCodes.Contains(instructions[i].opcode) &&
		//		   storeLocalCodes.Contains(instructions[i + 1].opcode))
		//		{
		//			// Remove i and i + 1
		//			instructions.RemoveRange(i, 2);
		//			removeCounter++;
		//		}
		//	}

		//	// Check if expected remove amount reached
		//	if (removeCounter != paramCount)
		//	{
		//		throw new SystemException(removeCounter + " IL parameter initialize statements removed while " + paramCount +" should have been");
		//	}
		//	return instructions;
		//}
	}
}


