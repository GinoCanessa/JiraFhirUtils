using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JiraFhirUtils.Common;
using Microsoft.Data.Sqlite;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Data;

namespace fmg_r6_review.Dict;

internal class DictLoader
{
    private const string _wordFilePattern = "*.words.txt";
    private const string _typoFilePattern = "*.typo.txt";

    private CliConfig _config;

    public DictLoader(CliConfig config)
    {
        _config = config ?? throw new ArgumentNullException(nameof(config));

        if (string.IsNullOrEmpty(_config.DictionarySourcePath) &&
            string.IsNullOrEmpty(_config.DictionarySourceTgz))
        {
            throw new ArgumentException("Either DictionarySourcePath or DictionarySourceTgz must be provided.");
        }

        if (string.IsNullOrEmpty(_config.DictionaryDbPath))
        {
            throw new ArgumentException("DictionaryDbPath must be provided.");
        }
    }

    public void LoadDictionary()
    {
        Console.WriteLine("Loading dictionary...");

        Console.WriteLine($"Using dictionary database: {_config.DictionaryDbPath}");

        if (!string.IsNullOrEmpty(_config.DictionarySourcePath))
        {
            Console.WriteLine($"Using dictionary source path: {_config.DictionarySourcePath}");
        }
        else
        {
            Console.WriteLine($"Using dictionary source tgz: {_config.DictionarySourceTgz}");
        }

        using SqliteConnection db = new($"Data Source={_config.DictionaryDbPath}");
        db.Open();

        // drop tables first
        DictWordRecord.DropTable(db);
        DictTypoRecord.DropTable(db);

        // create tables
        DictWordRecord.CreateTable(db);
        DictTypoRecord.CreateTable(db);

        // load from either the directory or the tgz file
        if (!string.IsNullOrEmpty(_config.DictionarySourcePath))
        {
            loadFromDirectory(db, _config.DictionarySourcePath!);
        }
        else if (!string.IsNullOrEmpty(_config.DictionarySourceTgz))
        {
            loadFromTgz(db, _config.DictionarySourceTgz!);
        }
        else
        {
            throw new InvalidOperationException("Neither DictionarySourcePath nor DictionarySourceTgz is provided.");
        }

    }

    private void loadFromTgz(IDbConnection db, string tgzPath)
    {
        if (!File.Exists(tgzPath))
        {
            throw new ArgumentException($"TGZ file does not exist: {tgzPath}");
        }

        using FileStream fs = new(tgzPath, FileMode.Open, FileAccess.Read);
        using GZipStream gzipStream = new GZipStream(fs, CompressionMode.Decompress);
        using TarReader tarReader = new TarReader(gzipStream, leaveOpen: false);

        TarEntry? entry;
        while ((entry = tarReader.GetNextEntry()) is not null)
        {
            if (entry.EntryType != TarEntryType.RegularFile)
            {
                continue;
            }

            if (entry.Name.EndsWith(_wordFilePattern, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Loading words from TGZ entry: {entry.Name}");

                using Stream? dataStream = entry.DataStream;
                if (dataStream is null)
                {
                    Console.WriteLine($"Skipping entry with null data stream: {entry.Name}");
                    continue;
                }

                using StreamReader sr = new StreamReader(dataStream);
                loadWordsFromFile(db, sr);

                continue;
            }

            if (entry.Name.EndsWith(_typoFilePattern, StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"Loading typos from TGZ entry: {entry.Name}");

                using Stream? dataStream = entry.DataStream;
                if (dataStream is null)
                {
                    Console.WriteLine($"Skipping entry with null data stream: {entry.Name}");
                    continue;
                }

                using StreamReader sr = new StreamReader(dataStream);
                loadTyposFromFile(db, sr);

                continue;
            }
        }
    }

    private void loadFromDirectory(IDbConnection db, string dirPath)
    {
        if (!Directory.Exists(dirPath))
        {
            throw new ArgumentException($"Directory does not exist: {dirPath}");
        }

        string[] wordFiles = Directory.GetFiles(dirPath, _wordFilePattern);
        foreach (string wf in wordFiles)
        {
            Console.WriteLine($"Loading words from file: {wf}");
            using StreamReader sr = new(wf);
            loadWordsFromFile(db, sr);
        }

        string[] typoFiles = Directory.GetFiles(dirPath, _typoFilePattern);
        foreach (string tf in typoFiles)
        {
            Console.WriteLine($"Loading typos from file: {tf}");
            using StreamReader sr = new(tf);
            loadTyposFromFile(db, sr);
        }
    }

    private void loadWordsFromFile(IDbConnection db, TextReader fs)
    {
        List<DictWordRecord> words = [];

        string? line;
        while ((line = fs.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line) || 
                line.StartsWith('#') ||
                line.StartsWith('!'))
            {
                continue;
            }

            words.Add(new()
            {
                //Id = DictWordRecord.GetIndex(),
                Word = line
            });
        }

        DictWordRecord.Insert(db, words, ignoreDuplicates: true, insertPrimaryKey: true);
    }

    private void loadTyposFromFile(IDbConnection db, TextReader fs)
    {
        List<DictTypoRecord> typos = [];
        string? line;
        while ((line = fs.ReadLine()) is not null)
        {
            if (string.IsNullOrWhiteSpace(line) ||
                line.StartsWith('#') ||
                line.StartsWith('!'))
            {
                continue;
            }
            string[] parts = line.Split("->", StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                Console.WriteLine($"Skipping invalid typo line: {line}");
                continue;
            }
            typos.Add(new()
            {
                //Id = DictTypoRecord.GetIndex(),
                Typo = parts[0],
                Correction = parts[1]
            });
        }

        DictTypoRecord.Insert(db, typos, ignoreDuplicates: true, insertPrimaryKey: true);
    }
}
