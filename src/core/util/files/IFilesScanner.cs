namespace core.util.files;

public interface IFilesScanner
{
  Task Visit(Predicate<ScanItem> scanSubtree);
}