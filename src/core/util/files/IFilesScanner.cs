namespace core.util.files;

public interface IFilesScanner
{
  Task Visit(string dir, Predicate<ScanItem> scanSubtree);
}