# .NET Dice Engine

Simple dice engine written in C#.

Embed into your project or use the CLI to play around.

## Supported operations

Arithmetic (`+`, `-`, `*`, `/`)
```text
> 1 + 5
6

> 42 * 21
882

> 2 + 2 * 5
12

> (2 + 2) * 5
20
```

Rolling dice (`<num>d<faces>`)
```text
> 1d6
2

> 1d6
5

> 1d20 + 5
22

> (8 * 3)d5
57

> 1d(42 / 2)
6

> (5d7)d(4d2)
37
```

Dice modifiers (`<num>d<faces>[<modifier><operator><argument>]`)
```text
> 6d6cs>4
2
```

Comparison (`==`, `!=`, `<`, `<=`, `>=`, `>`, `<condition> ? <true-branch> : <false-branch>`)
```text
> 2 > 1
True

> 6 < 1d8
False

> 6 < 1d8
True

> 1d8 > 4 ? 20 : 1
1

> 1d8 > 4 ? 20 : 1
20
```

Functions (`floor`, `ceil`, `min`, `max`)
```text
> 1.5
1.5

> floor(1.5)
1

> ceil(1.5)
2

> min(5, 2, 4)
2

> max(5, 2, 4)
5
```

## TODO

- Record dice rolls and show their result without applying all the math
- Modulo


## Misc

This code is heavily based on the Java code for Lox, the language from [http://www.craftinginterpreters.com], mainly chapter two.
Check it out if you want to lern how to build interpreters!