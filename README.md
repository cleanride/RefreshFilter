# Visual Studio Refresh

Visual Studio uses a filter structure for C projects which defines which files are part of the project and build path.
This helper application refreshes the file structure of the project's .vcproj and .vcprojfilter files to include all files and folders found.

The program start looking in the working directory for a project file, and otherwise in its parent directory.

If there is a .gitignore file found it is used to exclude folders from the processing end foldername with slash "/".
