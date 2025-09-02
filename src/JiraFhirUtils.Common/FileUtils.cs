using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JiraFhirUtils.Common;

public static class FileUtils
{

    public static string? FindRelativeDir(
        string? startDir,
        string dirName,
        bool throwIfNotFound = true)
    {
        string currentDir;

        if (string.IsNullOrEmpty(startDir))
        {
            if (dirName.StartsWith('~'))
            {
                currentDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

                if (dirName.Length > 1)
                {
                    dirName = dirName[2..];
                }
                else
                {
                    dirName = string.Empty;
                }
            }
            else
            {
                currentDir = Path.GetDirectoryName(AppContext.BaseDirectory) ?? string.Empty;
            }
        }
        else if (startDir.StartsWith('~'))
        {
            // check if the path was only the user dir or the user dir plus a separator
            if ((startDir.Length == 1) || (startDir.Length == 2))
            {
                currentDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }
            else
            {
                // skip the separator
                currentDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), startDir[2..]);
            }
        }
        else
        {
            currentDir = startDir;
        }

        string testDir = Path.Combine(currentDir, dirName);

        while (!Directory.Exists(testDir))
        {
            currentDir = Path.GetFullPath(Path.Combine(currentDir, ".."));

            if (currentDir == Path.GetPathRoot(currentDir))
            {
                if (throwIfNotFound)
                {
                    throw new DirectoryNotFoundException($"Could not find directory {dirName}!");
                }

                return null;
            }

            testDir = Path.Combine(currentDir, dirName);
        }

        return Path.GetFullPath(testDir);
    }

    public static string? FindRelativeFile(
        string? startDir,
        string filename,
        bool throwIfNotFound = true)
    {
        string currentFilename;

        if (string.IsNullOrEmpty(startDir))
        {
            if (filename.StartsWith('~'))
            {
                currentFilename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

                if (filename.Length > 1)
                {
                    filename = filename[2..];
                }
                else
                {
                    filename = string.Empty;
                }
            }
            else
            {
                currentFilename = Path.GetDirectoryName(AppContext.BaseDirectory) ?? string.Empty;
            }
        }
        else if (startDir.StartsWith('~'))
        {
            // check if the path was only the user dir or the user dir plus a separator
            if ((startDir.Length == 1) || (startDir.Length == 2))
            {
                currentFilename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
            }
            else
            {
                // skip the separator
                currentFilename = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), startDir[2..]);
            }
        }
        else
        {
            currentFilename = startDir;
        }

        string testFilename = Path.Combine(currentFilename, filename);

        while (!File.Exists(testFilename))
        {
            currentFilename = Path.GetFullPath(Path.Combine(currentFilename, ".."));

            if (currentFilename == Path.GetPathRoot(currentFilename))
            {
                if (throwIfNotFound)
                {
                    throw new DirectoryNotFoundException($"Could not find file {filename}!");
                }

                return null;
            }

            testFilename = Path.Combine(currentFilename, filename);
        }

        return Path.GetFullPath(testFilename);
    }

}
