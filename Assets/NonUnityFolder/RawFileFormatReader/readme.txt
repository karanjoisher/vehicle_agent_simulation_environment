Command line usage:

>python rawFileFormatHandler.py <path_to_raw_file OR path_to_a_folder> <number_of_threads_to_spawn>

Result:
If path to a file was given say abc.raw, then it will create abc_convertedToPNGs folder in the same directory
as abc.raw and it will contain all the pngs.

If path to a folder "foo" was given, it will create a folder "foo_convertedToPNGs" in the "foo" directory, and
it will have PNGs for all the ".raw" files present in foo. 

Note:
Preferably set number_of_threads_to_spawn to the number of cores on your machine.


There are functions in rawFileFormatHandler module that helps to create numpy images from ".raw". Check
the "exampleHowToUseRawFileFormatHandler.py" file to know how to use it.