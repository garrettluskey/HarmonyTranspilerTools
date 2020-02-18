# HarmonyTranspilerTools

Harmony Transpiler Tools is an easy to use IL instruction editor with features for the patching library [Harmony](https://github.com/pardeike/Harmony).

## Features

### Replace an entire function with another
```C#
public void ReplaceMethod(MethodInfo method)
```

***

<br />

### Place additional CodeBlock statements after all instances of codeBlockToFind statements.
```C#
public void AddCodeBlockAfter(MethodInfo codeBlockToFind, MethodInfo additionCodeBlock)
```
***

<br />

### Place additional CodeBlock statements before all instances of codeBlockToFind statements.
```C#
public void AddCodeBlockBefore(MethodInfo codeBlockToFind, MethodInfo additionCodeBlock)
```

***

<br />

### Replace all instances of codeBlockToFind with replacementCodeBlock.
```C#
public void ReplaceAllCodeBlocks(MethodInfo codeBlockToFind, MethodInfo replacementCodeBlock)
```

***

<br />

### Remove all instances of codeBlockToFind from instructions.
```C#
public void RemoveCodeBlock(MethodInfo codeBlockToRemove)
```
<br />


## Example
Let's say we have some compiled class with some method that we want to change the functionality of:
```C#
public class DummyCompiledClass
{
    public int ComplexMethod(int y)
    {
        for (int i = 0; i < 10; i++)
        {
            y++;
        }
        return y;
    }
}
```

In this example, let's change `y++` to `y = y + 2`.

To do this we need a few things, a transpiler, a code block to search for, and a code block to replace  `y++` with.

For the transpiler, we will be using macros to define it.

```C#
[HarmonyPatch(typeof(DummyCompiledClass))]
[HarmonyPatch("SomeMethod")]
public static class DummyHarmonyClass
{
    public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
    {

    }
}
```

This transpiler will be called when `harmony.PatchAll()` is called.

As for the method we will use to replace, we will need a class preferably inheriting from the compiled class. _See [Caveats and Class Variables](https://github.com/garrettluskey/HarmonyTranspilerTools/wiki/Caveats-and-Class-Variables) for this reason_. This class will contain the method used to search for `y++` and the method we want to use for replacement.

```C#
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
}
```

This class and method can be named anything as it will be referenced below in the transpiler. To replace `y++` we pass the _SearchStatement_ as MethodInfo to _ReplaceAllCodeBlocks_ as the first parameter. _ReplaceAllCodeBlocks_ will search for **all instances** of this statement and replace each instance with _ReplacementStatement_. This is done by searching the IL code for matching IL of _SearchStatement_ and replacing the statement with the IL of _ReplacementStatement_.

```C#
[HarmonyPatch(typeof(DummyCompiledClass))]
[HarmonyPatch("ComplexMethod")]
public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
{
    MethodInfo searchMethod = typeof(DummyModClass).GetMethod("SearchStatement");
    MethodInfo replacementMethod = typeof(DummyModClass).GetMethod("ReplacementStatement");

    ILTool parser = new ILTool(instr);

    parser.ReplaceAllCodeBlocks(searchMethod, replacementMethod);

    return parser.instructions;
}
```

Now after `harmony.PatchAll()` is called, _ComplexMethod_ will always return `y + 20` instead of the pre-patched `y + 10`.

## Example Code
```C#
using Harmony;
using HarmonyTranspilerTools;
// Remember to include the application you are patching

// Class from application you are modding
public class DummyCompiledClass
{
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
}

[HarmonyPatch(typeof(DummyCompiledClass))]
[HarmonyPatch("ComplexMethod")]
public static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instr)
{
    MethodInfo searchMethod = typeof(DummyCompiledClass).GetMethod("SomeMethod");
    MethodInfo replacementMethod = typeof(DummyModClass).GetMethod("SomeMethod");

    ILTool parser = new ILTool(instr);

    parser.ReplaceAllCodeBlocks(searchMethod, replacementMethod);

    return parser.instructions;
}
```
