﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Common.Core.Shell;
using Microsoft.R.Actions.Utility;
using Microsoft.R.Support.Test.Utility;
using Microsoft.UnitTests.Core.XUnit;
using Xunit;

namespace Microsoft.R.Host.Client.Test {
    [ExcludeFromCodeCoverage]
    [Collection(CollectionNames.NonParallel)]
    public class IdeGraphicsDeviceTest {
        private readonly GraphicsDeviceTestFilesFixture _files;
        private readonly MethodInfo _testMethod;

        // Copied from RSessionEvaluationCommands.cs
        // TODO: need to merge into a single location
        private const string SetupCode = @"
.rtvs.vsgdresize <- function(width, height) {
   invisible(.External('Microsoft.R.Host::External.ide_graphicsdevice_resize', width, height))
}
.rtvs.vsgd <- function() {
   invisible(.External('Microsoft.R.Host::External.ide_graphicsdevice_new'))
}
.rtvs.vsgdexportimage <- function(filename, device) {
    dev.copy(device=device,filename=filename)
    dev.off()
}
.rtvs.vsgdexportpdf <- function(filename) {
    dev.copy(device=pdf,file=filename)
    dev.off()
}
.rtvs.vsgdnextplot <- function() {
   invisible(.External('Microsoft.R.Host::External.ide_graphicsdevice_next_plot'))
}
.rtvs.vsgdpreviousplot <- function() {
   invisible(.External('Microsoft.R.Host::External.ide_graphicsdevice_previous_plot'))
}
.rtvs.vsgdhistoryinfo <- function() {
   .External('Microsoft.R.Host::External.ide_graphicsdevice_history_info')
}
options(device = '.rtvs.vsgd')
";

        private const int DefaultWidth = 360;
        private const int DefaultHeight = 360;

        private const int DefaultExportWidth = 480;
        private const int DefaultExportHeight = 480;

        private readonly Callbacks _callbacks;

        public IdeGraphicsDeviceTest(GraphicsDeviceTestFilesFixture files, TestMethodInfoFixture testMethod) {
            _files = files;
            _testMethod = testMethod.Method;
            _callbacks = new Callbacks(this);
        }

        private int X(double percentX) {
            return (int)(DefaultWidth * percentX);
        }

        private int Y(double percentY) {
            return (int)(DefaultHeight - DefaultHeight * percentY - 1);
        }

        [Test]
        [Category.Plots]
        public void GridLine() {
            var code = @"
library(grid)
grid.newpage()
grid.segments(.01, .1, .99, .1)
";
            var actualPlotFilePaths = GraphicsTest(code).ToArray();
            actualPlotFilePaths.Should().ContainSingle();

            var bmp = (Bitmap)Image.FromFile(actualPlotFilePaths[0]);
            bmp.Width.Should().Be(DefaultWidth);
            bmp.Height.Should().Be(DefaultHeight);
            var startX = X(0.01);
            var endX = X(0.99);
            var y = Y(0.1);

            var fg = Color.FromArgb(255, 0, 0, 0);
            var bg = Color.FromArgb(255, 255, 255, 255);

            // Check extremities on the line
            bmp.GetPixel(startX, y).Should().Be(fg);
            bmp.GetPixel(endX, y).Should().Be(fg);

            // Check extremities outside of line
            bmp.GetPixel(startX - 1, y).Should().Be(bg);
            bmp.GetPixel(endX + 1, y).Should().Be(bg);

            // Check above and below line
            bmp.GetPixel(startX, y - 1).Should().Be(bg);
            bmp.GetPixel(startX, y + 1).Should().Be(bg);
        }

        [Test]
        [Category.Plots]
        public void MultiplePagesWithSleep() {
            var code = @"
library(grid)
redGradient <- matrix(hcl(0, 80, seq(50, 80, 10)), nrow = 4, ncol = 5)

# interpolated
grid.newpage()
grid.raster(redGradient)
Sys.sleep(1)

# blocky
grid.newpage()
grid.raster(redGradient, interpolate = FALSE)
";
            GraphicsTest(code).Should().HaveCount(2);
        }

        [Test]
        [Category.Plots]
        public void MultiplePlotsWithSleep() {
            var code = @"
plot(0:10)
Sys.sleep(1)
plot(5:15)
";
            GraphicsTest(code).Should().HaveCount(2);
        }

        [Test]
        [Category.Plots]
        public void MultiplePlotsNoSleep() {
            var code = @"
plot(0:10)
plot(5:15)
";
            GraphicsTest(code).Should().ContainSingle();
        }

        [Test]
        [Category.Plots]
        public void PlotCars() {
            GraphicsTestAgainstExpectedFiles(@"plot(cars)");
        }

        [Test]
        [Category.Plots]
        public void SetInitialSize() {
            var code = @"
.rtvs.vsgdresize(600, 600)
plot(0:10)
";
            var actualPlotFilePaths = GraphicsTest(code).ToArray();
            actualPlotFilePaths.Should().ContainSingle();

            var bmp = (Bitmap)Image.FromFile(actualPlotFilePaths[0]);
            bmp.Width.Should().Be(600);
            bmp.Height.Should().Be(600);
        }

        [Test]
        [Category.Plots]
        public void ResizeImmediately() {
            var code = @"
plot(0:10)
.rtvs.vsgdresize(600, 600)
";
            var actualPlotFilePaths = GraphicsTest(code).ToArray();
            actualPlotFilePaths.Should().ContainSingle();

            var bmp = (Bitmap)Image.FromFile(actualPlotFilePaths[0]);
            bmp.Width.Should().Be(600);
            bmp.Height.Should().Be(600);
        }

        [Test]
        [Category.Plots]
        public void ResizeAfterDelay() {
            var code = @"
plot(0:10)
Sys.sleep(1)
.rtvs.vsgdresize(600, 600)
";
            var actualPlotFilePaths = GraphicsTest(code).ToArray();
            actualPlotFilePaths.Should().HaveCount(2);

            var bmp1 = (Bitmap)Image.FromFile(actualPlotFilePaths[0]);
            var bmp2 = (Bitmap)Image.FromFile(actualPlotFilePaths[1]);
            bmp1.Width.Should().Be(DefaultWidth);
            bmp1.Height.Should().Be(DefaultHeight);
            bmp2.Width.Should().Be(600);
            bmp2.Height.Should().Be(600);
        }

        [Test]
        [Category.Plots]
        public void ExportToImage() {
            var exportedBmpFilePath = _files.ExportToBmpResultPath;
            var exportedPngFilePath = _files.ExportToPngResultPath;
            var exportedJpegFilePath = _files.ExportToJpegResultPath;
            var exportedTiffFilePath = _files.ExportToTiffResultPath;

            var code = string.Format(@"
plot(0:10)
Sys.sleep(1)
.rtvs.vsgdexportimage('{0}', bmp)
.rtvs.vsgdexportimage('{1}', png)
.rtvs.vsgdexportimage('{2}', jpeg)
.rtvs.vsgdexportimage('{3}', tiff)
",
                exportedBmpFilePath.Replace("\\", "/"),
                exportedPngFilePath.Replace("\\", "/"),
                exportedJpegFilePath.Replace("\\", "/"),
                exportedTiffFilePath.Replace("\\", "/"));

            var actualPlotFilePaths = GraphicsTest(code).ToArray();
            actualPlotFilePaths.Should().ContainSingle();

            var bmp = (Bitmap)Image.FromFile(actualPlotFilePaths[0]);
            bmp.Width.Should().Be(DefaultWidth);
            bmp.Height.Should().Be(DefaultHeight);

            var exportedBmp = (Bitmap)Image.FromFile(exportedBmpFilePath);
            exportedBmp.Width.Should().Be(DefaultExportWidth);
            exportedBmp.Height.Should().Be(DefaultExportHeight);

            var exportedPng = (Bitmap)Image.FromFile(exportedPngFilePath);
            exportedPng.Width.Should().Be(DefaultExportWidth);
            exportedPng.Height.Should().Be(DefaultExportHeight);

            var exportedJpeg = (Bitmap)Image.FromFile(exportedJpegFilePath);
            exportedJpeg.Width.Should().Be(DefaultExportWidth);
            exportedJpeg.Height.Should().Be(DefaultExportHeight);

            var exportedTiff = (Bitmap)Image.FromFile(exportedTiffFilePath);
            exportedTiff.Width.Should().Be(DefaultExportWidth);
            exportedTiff.Height.Should().Be(DefaultExportHeight);
        }

        [Test]
        [Category.Plots]
        public void ExportToPdf() {
            var exportedFilePath = _files.ExportToPdfResultPath;

            var code = string.Format(@"
plot(0:10)
Sys.sleep(1)
.rtvs.vsgdexportpdf('{0}')
", exportedFilePath.Replace("\\", "/"));

            var actualPlotFilePaths = GraphicsTest(code).ToArray();
            actualPlotFilePaths.Should().ContainSingle();

            var bmp = (Bitmap)Image.FromFile(actualPlotFilePaths[0]);
            bmp.Width.Should().Be(DefaultWidth);
            bmp.Height.Should().Be(DefaultHeight);

            File.Exists(exportedFilePath).Should().BeTrue();
        }

        [Test]
        [Category.Plots]
        public void History() {
            var code = @"
plot(0:10)
Sys.sleep(1)
plot(5:15)
Sys.sleep(1)
.rtvs.vsgdpreviousplot()
";
            // TODO: Make this validate against a set of expected files to avoid any false positive
            var actualPlotFilePaths = GraphicsTest(code).ToArray();
            actualPlotFilePaths.Should().HaveCount(3);

            File.ReadAllBytes(actualPlotFilePaths[2]).Should().Equal(File.ReadAllBytes(actualPlotFilePaths[0]));
            File.ReadAllBytes(actualPlotFilePaths[1]).Should().NotEqual(File.ReadAllBytes(actualPlotFilePaths[0]));
        }

        [Test]
        [Category.Plots]
        public void HistoryInfo() {
            var code = @"
plot(0:10)
Sys.sleep(1)
plot(5:15)
Sys.sleep(1)
.rtvs.vsgdpreviousplot()
.rtvs.vsgdhistoryinfo()
";
            // TODO: Make this validate against a set of expected files to avoid any false positive
            var actualPlotFilePaths = GraphicsTest(code).ToArray();
            actualPlotFilePaths.Should().HaveCount(3);

            File.ReadAllBytes(actualPlotFilePaths[2]).Should().Equal(File.ReadAllBytes(actualPlotFilePaths[0]));
            File.ReadAllBytes(actualPlotFilePaths[1]).Should().NotEqual(File.ReadAllBytes(actualPlotFilePaths[0]));

            int expectedActive = 0;
            int expectedCount = 2;
            _callbacks.Output.Should().Be($"[1] {expectedActive} {expectedCount}\n\n");
        }

        [Test]
        [Category.Plots]
        public void HistoryResizeOldPlot() {
            var code = @"
plot(0:10)
Sys.sleep(1)
plot(5:15)
Sys.sleep(1)
.rtvs.vsgdresize(600, 600)
Sys.sleep(1)
.rtvs.vsgdpreviousplot()
Sys.sleep(1)
";
            // TODO: Make this validate against a set of expected files to avoid any false positive
            var actualPlotFilePaths = GraphicsTest(code).ToArray();
            actualPlotFilePaths.Should().HaveCount(4);

            var bmp1 = (Bitmap)Image.FromFile(actualPlotFilePaths[0]);
            var bmp2 = (Bitmap)Image.FromFile(actualPlotFilePaths[1]);
            var bmp3 = (Bitmap)Image.FromFile(actualPlotFilePaths[2]);
            var bmp4 = (Bitmap)Image.FromFile(actualPlotFilePaths[3]);
            bmp1.Width.Should().Be(DefaultWidth);
            bmp1.Height.Should().Be(DefaultHeight);
            bmp2.Width.Should().Be(DefaultWidth);
            bmp2.Height.Should().Be(DefaultHeight);
            bmp3.Width.Should().Be(600);
            bmp3.Height.Should().Be(600);
            bmp4.Width.Should().Be(600);
            bmp4.Height.Should().Be(600);
        }

        private void GraphicsTestAgainstExpectedFiles(string code) {
            var actualPlotPaths = GraphicsTest(code).ToArray();
            var expectedPlotPaths = GetTestExpectedFiles();
            actualPlotPaths.Should().BeEquivalentTo(expectedPlotPaths);

            foreach (string path in expectedPlotPaths) {
                var actualPlotPath = actualPlotPaths.First(p => Path.GetFileName(p) == Path.GetFileName(path));
                File.ReadAllBytes(actualPlotPath).Should().Equal(File.ReadAllBytes(path));
            }
        }

        private string[] GetTestExpectedFiles() {
            var folderPath = _files.DestinationPath;
            var expectedFilesFilter = $"{_testMethod.DeclaringType?.AssemblyQualifiedName}-{_testMethod.Name}-*.png";
            var expectedFiles = Directory.GetFiles(folderPath, expectedFilesFilter);
            return expectedFiles;
        }

        internal string SavePlotFile(string plotFilePath, int i) {
            var newFileName = $"{_testMethod.DeclaringType?.AssemblyQualifiedName}-{_testMethod.Name}-{i}{Path.GetExtension(plotFilePath)}";
            var testOutputFilePath = Path.Combine(_files.DestinationPath, newFileName);
            File.Copy(plotFilePath, testOutputFilePath);
            return testOutputFilePath;
        }

        private IEnumerable<string> GraphicsTest(string code) {
            _callbacks.SetInput(SetupCode + "\n" + code + "\n");
            var host = new RHost("Test", _callbacks);
            var rhome = RInstallation.GetLatestEnginePathFromRegistry();
            host.CreateAndRun(rhome, string.Empty).GetAwaiter().GetResult();

            // Ensure that all plot files created by the graphics device have been deleted
            foreach (var deletedFilePath in _callbacks.OriginalPlotFilePaths) {
                File.Exists(deletedFilePath).Should().BeFalse();
            }

            return _callbacks.PlotFilePaths.AsReadOnly();
        }

        class Callbacks : IRCallbacks {
            private readonly IdeGraphicsDeviceTest _testInstance;
            private readonly StringBuilder _output;
            private string _inputCode;

            public List<string> PlotFilePaths { get; }

            public List<string> OriginalPlotFilePaths { get; }

            public void SetInput(string code) {
                _inputCode = code;
            }

            public string Output => _output.ToString();

            public Callbacks(IdeGraphicsDeviceTest testInstance) {
                _testInstance = testInstance;
                _inputCode = string.Empty;
                PlotFilePaths = new List<string>();
                OriginalPlotFilePaths = new List<string>();
                _output = new StringBuilder();
            }

            public Task Busy(bool which, CancellationToken ct) {
                return Task.FromResult(true);
            }

            public Task Connected(string rVersion) {
                return Task.CompletedTask;
            }

            public Task Disconnected() {
                return Task.CompletedTask;
            }

            public Task<string> ReadConsole(IReadOnlyList<IRContext> contexts, string prompt, int len, bool addToHistory, bool isEvaluationAllowed, CancellationToken ct) {
                // We're getting called a few times here
                // First time, send over the code to execute
                // After that, send nothing
                var code = _inputCode;
                _inputCode = "";
                return Task.FromResult(code);
            }

            public async Task ShowMessage(string s, CancellationToken ct) {
                await Console.Error.WriteLineAsync(s);
            }

            public async Task WriteConsoleEx(string buf, OutputType otype, CancellationToken ct) {
                _output.Append(buf);
                var writer = otype == OutputType.Output ? Console.Out : Console.Error;
                await writer.WriteAsync(buf);
            }

            public Task<YesNoCancel> YesNoCancel(IReadOnlyList<IRContext> contexts, string s, bool isEvaluationAllowed, CancellationToken ct) {
                return Task.FromResult(Client.YesNoCancel.Yes);
            }

            public Task Plot(string filePath, CancellationToken ct) {
                if (filePath.Length > 0) {
                    // Make a copy of the plot file, and store the path to the copy
                    // When the R code finishes executing, the graphics device is destructed,
                    // which destructs all the plots which deletes the original plot files
                    int index = PlotFilePaths.Count;
                    PlotFilePaths.Add(_testInstance.SavePlotFile(filePath, index));

                    // We also store the original plot file paths, so we can 
                    // validate that they have been deleted when the host goes away
                    OriginalPlotFilePaths.Add(filePath);
                }
                return Task.CompletedTask;
            }

            public Task Browser(string url) {
                throw new NotImplementedException();
            }

            public void DirectoryChanged() {
                throw new NotImplementedException();
            }

            public Task<MessageButtons> ShowDialog(IReadOnlyList<IRContext> contexts, string s, bool isEvaluationAllowed, MessageButtons buttons, CancellationToken ct) {
                throw new NotImplementedException();
            }
        }
    }
}
