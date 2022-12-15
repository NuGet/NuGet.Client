# System.CommandLine code geneartion

We use T4 templates to generate System.Commandline boilerplate code.

Miscelaneous files:

- Utils.tt : Contains helper functioins for the T4 templates.

The templates that actually generate code are:

- CommandParsers.tt: This template generates a class with a method to populate options to System.CommandLine root command
- CustomBinders.tt This template generates boilerplate code to maps command arguments to **Args objects, in this case AddSourceArgs
- VerbParsers.tt: This generates the boilerplate code for each expected option/argument in each command