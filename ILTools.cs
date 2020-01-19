using System;
using System.Collections.Generic;
using Harmony;
using Harmony.ILCopying;
using System.Reflection.Emit;
using System.Reflection;

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

		public override string ToString()
		{
			return start.ToString() + ", " + end.ToString();
		}
	}

    public class ILTool
    {
		private static MethodInfo MIReplaceShortJumps = typeof(CodeTranspiler).GetMethod("ReplaceShortJumps", BindingFlags.NonPublic | BindingFlags.Static);
		private static FieldInfo FIilInstructions = typeof(MethodBodyReader).GetField("ilInstructions", BindingFlags.NonPublic | BindingFlags.Instance);

		/// <summary>
		/// Compiles a method into IL instructions.
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
			DynamicMethod dynamicMethod = DynamicTools.CreateDynamicMethod(method, "_ILParser");
			if (dynamicMethod == null)
			{
				return null;
			}

			// Get il generato
			ILGenerator il = dynamicMethod.GetILGenerator();
			LocalBuilder[] existingVariables = DynamicTools.DeclareLocalVariables(method, il);
			Dictionary<string, LocalBuilder> privateVars = new Dictionary<string, LocalBuilder>();

			MethodBodyReader reader = new MethodBodyReader(method, il);
			reader.DeclareVariables(existingVariables);
			reader.ReadInstructions();

			List<ILInstruction> ilInstructions = (List<ILInstruction>)FIilInstructions.GetValue(reader);

			// Defines function start label
			il.DefineLabel();

			// Define labels
			foreach (ILInstruction ilInstruction in ilInstructions)
			{
				switch (ilInstruction.opcode.OperandType)
				{
					case OperandType.InlineSwitch:
						{
							ILInstruction[] array = ilInstruction.operand as ILInstruction[];
							if (array != null)
							{
								List<Label> labels = new List<Label>();
								ILInstruction[] array2 = array;
								foreach (ILInstruction iLInstruction2 in array2)
								{
									Label item = il.DefineLabel();
									iLInstruction2.labels.Add(item);
									labels.Add(item);
								}
								ilInstruction.argument = labels.ToArray();
							}
							break;
						}
					case OperandType.InlineBrTarget:
					case OperandType.ShortInlineBrTarget:
						{
							ILInstruction iLInstruction = ilInstruction.operand as ILInstruction;
							if (iLInstruction != null)
							{
								Label label2 = il.DefineLabel();
								iLInstruction.labels.Add(label2);
								ilInstruction.argument = label2;
							}
							break;
						}
				}
			}

			// Transpile code
			CodeTranspiler codeTranspiler = new CodeTranspiler(ilInstructions);
			List<CodeInstruction> result = codeTranspiler.GetResult(il, method);

			// Replace debug commands with normal
			foreach (CodeInstruction codeInstruction in result)
			{
				OpCode opCode = codeInstruction.opcode;

				codeInstruction.opcode = ReplaceShortJumps(opCode);
			}

			return result;

		}

		/// <summary>
		/// Replaces debug commands with matching non-debug commands
		/// </summary>
		/// <param name="opCode">Code to change</param>
		/// <returns>Non-debug opcode</returns>
		public static OpCode ReplaceShortJumps(OpCode opCode)
		{
			return (OpCode)MIReplaceShortJumps.Invoke(null, new object[] { opCode });
		}

		public List<CodeInstruction> instructions { get; private set; }
		public ILParser(List<CodeInstruction> instructions)
		{
			this.instructions = instructions;
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
		/// Replaces all instances of codeBlockToFind with replacementCodeBlock
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
		/// Removes given code block from instructions
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
		public static readonly List<OpCode> loadArgCodes = new List<OpCode>()
		{
			OpCodes.Ldarg,
			OpCodes.Ldarga,
			OpCodes.Ldarg_0,
			OpCodes.Ldarg_1,
			OpCodes.Ldarg_2,
			OpCodes.Ldarg_3,
			OpCodes.Ldarg_S,
		};

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

		public static readonly List<OpCode> storeLocalCodes = new List<OpCode>()
		{
			OpCodes.Stloc,
			OpCodes.Stloc_0,
			OpCodes.Stloc_1,
			OpCodes.Stloc_2,
			OpCodes.Stloc_3,
			OpCodes.Stloc_S,
		};
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


