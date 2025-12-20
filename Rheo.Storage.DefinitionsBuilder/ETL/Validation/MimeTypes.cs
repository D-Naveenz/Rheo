using Rheo.Storage.DefinitionsBuilder.Settings;

namespace Rheo.Storage.DefinitionsBuilder.ETL.Validation
{
    internal static class MimeTypes
    {
        private const string MIME_TYPES_PATH_NAME = "MimeTypes";

        /// <summary>
        /// Loads and returns a set of MIME types by reading and merging data from CSV files in the specified directory.
        /// </summary>
        /// <remarks>This method searches for CSV files in the directory defined by the <see
        /// cref="Configuration.AssetsPath"/> combined with the MIME types path name. It extracts the MIME types from
        /// the third column of each CSV file and merges them into a single set. Duplicate MIME types are automatically
        /// removed.</remarks>
        /// <returns>A <see cref="HashSet{T}"/> containing the unique MIME types extracted from the CSV files. If no CSV files
        /// are found, the returned set will be empty.</returns>
        public static HashSet<string> Load()
        {
            var exclusionListsPath = Path.Combine(Configuration.AssetsPath, MIME_TYPES_PATH_NAME);

            var csvFiles = Directory.GetFiles(exclusionListsPath, "*.csv", SearchOption.TopDirectoryOnly);

            List<List<string>> mimeTypeLists = [];
            foreach (var csvFile in csvFiles)
            {
                // Parse CSV and extract mime types column
                var mimeTypes = ParseCSV(csvFile).GetDataColumn(1);
                mimeTypeLists.Add(mimeTypes);
            }
            return MergeColumns([.. mimeTypeLists]);
        }

        /// <summary>
        /// Extracts the data from a specified column in a CSV-like structure, excluding the header row.
        /// </summary>
        /// <remarks>This method assumes that the first row of the input data represents the header and
        /// skips it when extracting column data. Rows with fewer columns than the specified <paramref
        /// name="columnNumber"/> are ignored.</remarks>
        /// <param name="csv">The list of string arrays representing the rows of the CSV-like data. Each array corresponds to a row, and
        /// each element in the array represents a column value.</param>
        /// <param name="columnNumber">The zero-based index of the column to extract. Must be within the bounds of the column indices in the data
        /// rows.</param>
        /// <returns>A list of strings containing the values from the specified column, excluding the header row. If the column
        /// index is out of bounds for a row, that row is skipped.</returns>
        private static List<string> GetDataColumn(this List<string[]> csv, int columnNumber)
        {
            List<string> columnData = [];
            // Start from 1 to skip header row
            for (int i = 1; i < csv.Count; i++)
            {
                var row = csv[i];
                if (row.Length > columnNumber)
                {
                    columnData.Add(row[columnNumber]);
                }
            }

            return columnData;
        }

        /// <summary>
        /// Merges multiple lists of strings into a single set containing unique values.
        /// </summary>
        /// <remarks>This method combines all elements from the provided lists into a single set, ensuring
        /// that no duplicates are present in the result.</remarks>
        /// <param name="args">An array of lists of strings to be merged. Each list can contain duplicate values, which will be removed in
        /// the resulting set.</param>
        /// <returns>A <see cref="HashSet{T}"/> containing the unique strings from all input lists.</returns>
        private static HashSet<string> MergeColumns(params List<string>[] args)
        {
            HashSet<string> mergedList = [];
            foreach (var array in args)
            {
                mergedList.UnionWith(array);
            }
            return mergedList;
        }

        /// <summary>
        /// Parses a CSV file and returns its contents as a list of string arrays.
        /// </summary>
        /// <remarks>This method reads all lines from the specified CSV file and splits each line into
        /// columns based on commas. Empty columns are removed from the resulting arrays. The method does not handle
        /// quoted fields or other advanced CSV features.</remarks>
        /// <param name="path">The file path of the CSV file to parse. The file must exist and be accessible.</param>
        /// <returns>A list of string arrays, where each array represents a row in the CSV file, and each element in the array
        /// represents a column value. Empty columns are excluded.</returns>
        private static List<string[]> ParseCSV(string path)
        {
            List<string[]> csv = [];
            var lines = File.ReadAllLines(path);

            // Initialize a 2D structure for store csv data
            foreach (var line in lines)
            {
                csv.Add(line.Split(',', StringSplitOptions.RemoveEmptyEntries));
            }
            return csv;
        }
    }
}
