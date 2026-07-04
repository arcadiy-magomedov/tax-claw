using System.Formats.Tar;
using System.IO.Compression;
using System.Text;
using TaxClaw.Documents.Batch;

namespace TaxClaw.Documents.Tests;

public class DocumentBatchResolverTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("taxclaw-batch-").FullName;

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Theory]
    [InlineData("statements.zip")]
    [InlineData("statements.tar")]
    [InlineData("statements.tar.gz")]
    [InlineData("statements.tgz")]
    [InlineData("STATEMENTS.ZIP")]
    public void Recognizes_supported_archive_extensions(string name) =>
        Assert.True(DocumentBatchResolver.IsArchive(name));

    [Theory]
    [InlineData("statement.pdf")]
    [InlineData("statement.txt")]
    [InlineData("archive")]
    public void Does_not_treat_ordinary_files_as_archives(string name) =>
        Assert.False(DocumentBatchResolver.IsArchive(name));

    [Fact]
    public async Task Resolves_a_single_file_as_one_document()
    {
        string path = Path.Combine(_root, "dividend.txt");
        await File.WriteAllTextAsync(path, "dividend statement");

        var docs = await DocumentBatchResolver.ResolveFileAsync(path).ToListAsync();

        var doc = Assert.Single(docs);
        Assert.Equal("dividend.txt", doc.Name);
        Assert.Equal("dividend statement", Encoding.UTF8.GetString(doc.Bytes));
    }

    [Fact]
    public async Task Scans_a_directory_recursively_and_skips_hidden_entries_and_unknown_extensions()
    {
        Directory.CreateDirectory(Path.Combine(_root, "2027", "brokerage"));
        Directory.CreateDirectory(Path.Combine(_root, ".git"));
        await File.WriteAllTextAsync(Path.Combine(_root, "dividend.txt"), "top-level");
        await File.WriteAllTextAsync(Path.Combine(_root, "2027", "brokerage", "trade.csv"), "nested");
        await File.WriteAllTextAsync(Path.Combine(_root, ".DS_Store"), "junk");
        await File.WriteAllTextAsync(Path.Combine(_root, "README.md"), "not a document");
        await File.WriteAllTextAsync(Path.Combine(_root, ".git", "config"), "hidden dir");

        var docs = await DocumentBatchResolver.ResolveDirectoryAsync(_root).ToListAsync();

        Assert.Equal(2, docs.Count);
        Assert.Contains(docs, d => d.Name == "dividend.txt");
        Assert.Contains(docs, d => d.Name == "2027/brokerage/trade.csv");
    }

    [Fact]
    public async Task Reads_recognized_entries_out_of_a_zip_archive_in_memory()
    {
        string zipPath = Path.Combine(_root, "statements.zip");
        using (FileStream fs = File.Create(zipPath))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            WriteEntry(zip, "dividend.txt", "dividend");
            zip.CreateEntry("folder/"); // directory entry — must be skipped
            WriteEntry(zip, "folder/trade.csv", "trade");
            WriteEntry(zip, "README.md", "skip me");
        }

        var docs = await DocumentBatchResolver.ResolveArchiveAsync(zipPath).ToListAsync();

        Assert.Equal(2, docs.Count);
        Assert.Contains(docs, d => d.Name == "dividend.txt" && Encoding.UTF8.GetString(d.Bytes) == "dividend");
        Assert.Contains(docs, d => d.Name == "folder/trade.csv" && Encoding.UTF8.GetString(d.Bytes) == "trade");
    }

    private static void WriteEntry(ZipArchive zip, string name, string content)
    {
        using Stream entryStream = zip.CreateEntry(name, CompressionLevel.NoCompression).Open();
        byte[] bytes = Encoding.UTF8.GetBytes(content);
        entryStream.Write(bytes);
    }

    [Fact]
    public async Task Reads_recognized_entries_out_of_a_tar_gz_archive_in_memory()
    {
        string tarGzPath = Path.Combine(_root, "statements.tar.gz");
        await using (FileStream fs = File.Create(tarGzPath))
        await using (var gzip = new GZipStream(fs, CompressionMode.Compress))
        await using (var writer = new TarWriter(gzip))
        {
            var entry = new PaxTarEntry(TarEntryType.RegularFile, "dividend.txt")
            {
                DataStream = new MemoryStream(Encoding.UTF8.GetBytes("dividend"))
            };
            await writer.WriteEntryAsync(entry);
        }

        var docs = await DocumentBatchResolver.ResolveArchiveAsync(tarGzPath).ToListAsync();

        var doc = Assert.Single(docs);
        Assert.Equal("dividend.txt", doc.Name);
        Assert.Equal("dividend", Encoding.UTF8.GetString(doc.Bytes));
    }
}

file static class AsyncEnumerableExtensions
{
    public static async Task<List<T>> ToListAsync<T>(this IAsyncEnumerable<T> source)
    {
        var list = new List<T>();
        await foreach (T item in source)
        {
            list.Add(item);
        }
        return list;
    }
}
