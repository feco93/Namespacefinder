using System.Reflection;
using System.Text.RegularExpressions;

class Program
{
  // Root namespace we care about
  const string RootNamespace = "";
  static readonly string[] ExcludedNamespaces =
  [
  ];

  static void Main(string[] args)
  {
    if (args.Length < 2)
    {
      Console.WriteLine("Usage: CompareNamespaces <assembly.dll> <file-with-namespaces>");
      return;
    }

    string assemblyPath = args[0];
    string textFilePath = args[1];

    if (!File.Exists(assemblyPath))
    {
      Console.WriteLine($"Assembly not found: {assemblyPath}");
      return;
    }
    if (!File.Exists(textFilePath))
    {
      Console.WriteLine($"File not found: {textFilePath}");
      return;
    }

    var allAssemblyNamespaces = GetNamespacesFromAssembly(assemblyPath).ToList();

    // ✅ Keep only namespaces under the given root
    var filteredAssemblyNamespaces = allAssemblyNamespaces
        .Where(ns => ns.StartsWith(RootNamespace + ".", StringComparison.Ordinal) &&
        !ExcludedNamespaces.Any(excluded => ns.StartsWith(excluded)))
        .ToList();

    // ✅ Only leaf namespaces
    var assemblyNamespaces = GetLeafNamespaces(filteredAssemblyNamespaces).ToList();

    var fileNamespaces = GetNamespacesFromFile(textFilePath).ToList();

    // Find assembly leaf namespaces NOT covered by file namespaces
    var notInFile = assemblyNamespaces
        .Where(ns => !IsCoveredByAny(ns, fileNamespaces))
        .OrderBy(n => n)
        .ToList();

    Console.WriteLine($"Assembly: {Path.GetFileName(assemblyPath)}");
    Console.WriteLine($"Total assembly namespaces (all): {allAssemblyNamespaces.Count}");
    Console.WriteLine($"Filtered under root '{RootNamespace}': {filteredAssemblyNamespaces.Count}");
    Console.WriteLine($"Leaf namespaces considered: {assemblyNamespaces.Count}");
    Console.WriteLine($"Namespaces in file: {fileNamespaces.Count}");
    Console.WriteLine();

    Console.WriteLine("=== Assembly leaf namespaces NOT present in file ===");
    if (!notInFile.Any())
    {
      Console.WriteLine("None 🎉 (every assembly leaf namespace under root is covered by the file)");
    }
    else
    {
      foreach (var ns in notInFile)
        Console.WriteLine(ns);
    }
  }

  static IEnumerable<string> GetNamespacesFromAssembly(string assemblyPath)
  {
    try
    {
      var assembly = Assembly.LoadFrom(assemblyPath);
      Type[] types;
      try
      {
        types = assembly.GetTypes();
      }
      catch (ReflectionTypeLoadException ex)
      {
        types = ex.Types.Where(t => t != null).ToArray();
        Console.WriteLine("Warning: some types couldn't be loaded; continuing with available types.");
      }

      return types
          .Where(t => !string.IsNullOrEmpty(t.Namespace))
          .Select(t => t.Namespace)
          .Distinct(StringComparer.Ordinal)
          .OrderBy(ns => ns, StringComparer.Ordinal)
          .ToList();
    }
    catch (Exception ex)
    {
      Console.WriteLine("Error loading assembly: " + ex.Message);
      return Enumerable.Empty<string>();
    }
  }

  static IEnumerable<string> GetLeafNamespaces(IEnumerable<string> namespaces)
  {
    var nsList = namespaces.ToList();
    return nsList.Where(ns => !nsList.Any(other =>
        other.Length > ns.Length &&
        other.StartsWith(ns + ".", StringComparison.Ordinal)));
  }

  static IEnumerable<string> GetNamespacesFromFile(string path)
  {
    var text = File.ReadAllText(path);
    var regex = new Regex(@"namespace(?::'([^']+)'|=+([A-Za-z_][A-Za-z0-9_.]*))", RegexOptions.Compiled);
    return regex.Matches(text)
                .Cast<Match>()
                .Select(m => m.Groups[1].Success ? m.Groups[1].Value.Trim() : m.Groups[2].Value.Trim())
                .Where(s => !string.IsNullOrEmpty(s))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(ns => ns, StringComparer.Ordinal)
                .ToList();
  }

  // Coverage rule: file namespace must equal ns OR be a parent of ns
  static bool IsCoveredByAny(string ns, IEnumerable<string> fileNamespaces)
  {
    foreach (var f in fileNamespaces)
    {
      if (string.Equals(f, ns, StringComparison.Ordinal))
        return true;

      if (ns.StartsWith(f + ".", StringComparison.Ordinal))
        return true; // file is a parent of assembly namespace
    }
    return false;
  }
}
