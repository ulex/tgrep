namespace core.util.files;

public record struct ScanItem(long ModStamp, bool IsDirectory, string Path);