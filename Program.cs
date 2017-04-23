using System;
using System.IO;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Drawing;

namespace SpaceCompare
{
    /// <summary>
    /// Loads data from a SpaceSniffer report in the "Grouped by folder" format.
    /// </summary>
    public class SpaceSnifferExportLoader {
        /// <summary>
        /// The directory data loaded from the file path given in the
        /// constructor.
        /// </summary>
        public LoadedDirectoryData Data { get; set; } = new LoadedDirectoryData();

        /// <summary>
        /// Creates a new instance of SpaceSnifferExportLoader and gathers data
        /// from the given file path.
        /// </summary>
        /// <param name="pathToExport">The file path to load data from.</param>
        public SpaceSnifferExportLoader(string pathToExport) {
            Console.WriteLine("Starting search...");
            // This could be much faster if using a Parallel.ForEach, but that
            // causes inconsistent results.
            foreach (var line in File.ReadLines(pathToExport)) {
                // Match lines which contain a folder definition
                var match = Regex.Match(line, @"(.*):(.*) \[(.*)\]");
                if (match.Success) {
                    var drive = match.Groups[1];
                    var path = match.Groups[2];
                    var fileSize = match.Groups[3];

                    Data.AddDirectory($"{drive}:{path}", fileSize.Value);
                }
            }
        }
    }

    /// <summary>
    /// Handles storing, parsing and analysing data from a SpaceSniffer report.
    /// </summary>
    public class LoadedDirectoryData {
        /// <summary>
        /// A dictionary mapping file paths to their sizes specified in a
        /// SpaceSniffer report.
        /// </summary>
        public Dictionary<string, long> DirectorySizes { get; set; } = new Dictionary<string, long>();

        /// <summary>
        /// Given a file size in any formats like the following:
        /// 3.2MB
        /// 67TB
        /// 4b
        /// Converts it to a long in bytes.
        /// </summary>
        /// <param name="formattedFileSize">The formatted file size.</param>
        /// <returns>A representation, as a long, of the file size.</returns>
        public long FormattedFileSizeToLong(string formattedFileSize) {
            // Extract the number before the decimal point, the number after it,
            // and the unit at the end of the string
            var match = Regex.Match(formattedFileSize, @"(\d+)\.?(\d+)?(.+)");
            var integerPart = match.Groups[1].Value;
            var decimalPart = match.Groups[2].Value;
            var sizeUnit = match.Groups[3].Value;

            // Parse into a double
            var fileSizeInSizeUnit = double.Parse($"{integerPart}.{decimalPart}");

            // Multiply this long so that its value is in bytes, based on the
            // unit at the end of the input string
            long multiplier = 0;
            switch (sizeUnit) {
                case "b":
                    multiplier = 1;
                    break;
                case "KB":
                    multiplier = 1000;
                    break;
                case "MB":
                    multiplier = 1000000;
                    break;
                case "GB":
                    multiplier = 1000000000;
                    break;
                case "TB":
                    multiplier = 1000000000000;
                    break;
            }
            if (multiplier == 0) {
                throw new ArgumentException("Invalid size unit");
            }

            return (long)(fileSizeInSizeUnit * multiplier);
        }

        /// <summary>
        /// Adds a newly scanned directory to this class' storage, allowing its
        /// increase in size to be analysed later. Also converts the file size
        /// from a string format such as "3MB" into a numeric size.
        /// </summary>
        /// <param name="path">The directory path which the size corresponds to.</param>
        /// <param name="formattedFileSize">The size of the directory.</param>
        public void AddDirectory(string path, string formattedFileSize) {
            DirectorySizes.Add(path, FormattedFileSizeToLong(formattedFileSize));
        }

        /// <summary>
        /// Compares this data to that of an older report, returning a dictionary
        /// mapping directories to their file size increase.
        /// </summary>
        /// <param name="other">The older export.</param>
        /// <remarks>
        /// If a file did not exist in the older report but does exist in the
        /// new report, it is considered to have increased to its current size
        /// from 0 bytes.
        /// </remarks>
        /// <returns>
        /// A dictionary with keys of file paths and values of file size
        /// increases in bytes.
        /// </returns>
        public Dictionary<string, long> DifferencesFromPreviousExport(LoadedDirectoryData other) {
            // Create a dictionary with the differences in size
            return this.DirectorySizes.AsParallel().Select(current => {
                try {
                    var previousValue = other.DirectorySizes[current.Key];
                    var difference = current.Value - previousValue;
                    return new KeyValuePair<string, long>(current.Key, difference);
                } catch (KeyNotFoundException) {
                    // If it is not present in the old report but present in the
                    // new one, it is a newly created file
                    return new KeyValuePair<string, long>(current.Key, current.Value);
                }
            }).Where(diff => diff.Value > 0)
              .ToDictionary(pair => pair.Key, pair => pair.Value);
        }
    }

    public class MainForm : Form {
        enum ExportKind { Older, Newer }

        private SpaceSnifferExportLoader _older;
        private Button _oldExportChooseButton;
        private SpaceSnifferExportLoader _newer;
        private Button _newExportChooseButton;
        private ListBox _listBox;

        /// <summary>
        /// Fired when either the "Choose Older Export" or "Choose Newer Export"
        /// button is clicked.
        /// </summary>
        /// <param name="kind">Which button was clicked.</param>
        private void PickExport(ExportKind kind) {
            var fd = new OpenFileDialog();
            if (fd.ShowDialog() == DialogResult.OK) {
                // Update the button text and set class fields
                if (kind == ExportKind.Older) {
                    _older = new SpaceSnifferExportLoader(fd.FileName);
                    var msg = $"Older Export Loaded ({_older.Data.DirectorySizes.Count} directories)";
                    _oldExportChooseButton.Text = msg;
                } else {
                    _newer = new SpaceSnifferExportLoader(fd.FileName);
                    var msg = $"Newer Export Loaded ({_newer.Data.DirectorySizes.Count} directories)";
                    _newExportChooseButton.Text = msg;
                }

                if (_older != null && _newer != null) {
                    // If both files have been loaded, we're ready to load data
                    // into our _listBox
                    var rawDifferences = _newer.Data.DifferencesFromPreviousExport(_older.Data);
                    var orderedDifferences = rawDifferences.ToList().OrderByDescending(x => x.Value);
                    var formattedDifferences = orderedDifferences.Select(x => 
                        $"{x.Key} - increased ~{x.Value} bytes");
                    foreach (var difference in formattedDifferences) {
                        _listBox.Items.Add(difference);
                    }
                }
            };
        }

        public MainForm() {
            Width = 500;
            Height = 800;
            Controls.Add(new Label {
                Text = @"Welcome!
To begin, choose your two exports below.
---- NOTE ----
After choosing an export, Windows will think this program has crashed and offer to close it.
It hasn't actually crashed! Don't close it. It's just taking a while and is actually gathering data
behind Windows' error.",
                Width = 500,
                Height = 100
            });

            _oldExportChooseButton = new Button {
                Text = "Choose Older Export...",
                Width = 450,
                Location = new Point(25, 120)
            };
            _oldExportChooseButton.Click += (o, e) => PickExport(ExportKind.Older);
            Controls.Add(_oldExportChooseButton);

            _newExportChooseButton = new Button {
                Text = "Choose Newer Export...",
                Width = 450,
                Location = new Point(25, 160)
            };
            _newExportChooseButton.Click += (o, e) => PickExport(ExportKind.Newer);
            Controls.Add(_newExportChooseButton);

            _listBox = new ListBox {
                Width = 450,
                Location = new Point(25, 200),
                Height = 550
            };
            _listBox.Click += (o, e) => MessageBox.Show(_listBox.SelectedItem.ToString());
            Controls.Add(_listBox);
        }
    }

    class Program
    {
        [STAThread]
        public static void Main(string[] args)
        {
            Application.Run(new MainForm());
            return;
        }
    }
}
