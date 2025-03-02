﻿using FluentAssertions;
using MergeSolutions.Core.Models;
using MergeSolutions.Core.Parsers;
using Xunit;

namespace MergeSolutions.Tests
{
    public class MergeSolutionsTests
    {
        [Fact]
        public void ExcludeSolutionsWithNoNonDirProjectsSelected()
        {
            var outDir = Path.Combine(Path.GetTempPath(), nameof(MergeSolutionsTests));
            RecursiveDelete(new DirectoryInfo(outDir));
            Directory.CreateDirectory(outDir);
            const string outSolutionName = "out.sln";

            var solutionPaths = new[] {"TestData/SolutionA/SolutionA.sln", "TestData/SolutionB/SolutionB.sln"};

            var outputSolutionPath = Path.Combine(outDir, outSolutionName);
            var contents = new List<string>();
            for (var i = 0; i < 2; i++)
            {
                var mergedSolution = SolutionInfo.MergeSolutions(Path.GetFileNameWithoutExtension(outputSolutionPath),
                    Path.GetDirectoryName(outputSolutionPath) ?? "",
                    out _,
                    p => !(p is Project && p.ProjectInfo.SolutionInfo?.Name == "SolutionB"),
                    null,
                    solutionPaths.Select(n => SolutionInfo.Parse(n)).ToArray());
                mergedSolution.Save();
                contents.Add(File.ReadAllText(outputSolutionPath));
            }

            //Require idempotence
            contents[1].Should().Be(contents[0]);

            var solutionInfo = SolutionInfo.Parse(outputSolutionPath);
            solutionInfo.NestedSection.Dirs.Should().ContainSingle(d => d.Name == "SolutionA");
            solutionInfo.NestedSection.Dirs.Should().NotContain(d => d.Name == "SolutionB");
        }

        [Fact]
        public void MergeOfSingleA()
        {
            var outDir = Path.Combine(Path.GetTempPath(), nameof(MergeSolutionsTests));
            RecursiveDelete(new DirectoryInfo(outDir));
            Directory.CreateDirectory(outDir);
            const string outSolutionName = "out.sln";

            var solutionPaths = new[] {"TestData/SolutionA/SolutionA.sln",};

            var outputSolutionPath = Path.Combine(outDir, outSolutionName);

            var contents = new List<string>();
            for (var i = 0; i < 2; i++)
            {
                var mergedSolution = SolutionInfo.MergeSolutions(Path.GetFileNameWithoutExtension(outputSolutionPath),
                    Path.GetDirectoryName(outputSolutionPath) ?? "",
                    out _,
                    null,
                    null,
                    solutionPaths.Select(n => SolutionInfo.Parse(n)).ToArray());
                mergedSolution.Save();
                contents.Add(File.ReadAllText(outputSolutionPath));
            }

            //Require idempotence
            contents[1].Should().Be(contents[0]);

            var solutionInfo = SolutionInfo.Parse(outputSolutionPath);
            solutionInfo.ExtensibilityGlobalsSection.SolutionGuid.Should().Be("{CAE28B69-4C6B-4930-95FE-1053358566FF}");
            solutionInfo.Projects.Should().HaveCount(6);
            solutionInfo.NestedSection.Dirs.Should().HaveCount(2);
            solutionInfo.NestedSection.Dirs.Should()
                .Contain(p => p.Guid == "{8FE39D73-9DBF-47A0-94E3-24F96625B4EA}");
            var inFolderDir = solutionInfo.NestedSection.Dirs.Single(d => d.Guid == "{8FE39D73-9DBF-47A0-94E3-24F96625B4EA}");
            inFolderDir.NestedProjects.Should()
                .Contain(p => p.Project.Guid == "{177D7443-2536-4B05-8CBD-5FAD3CE75FD3}");
            inFolderDir.NestedProjects.Should()
                .Contain(p => p.Project.Guid == "{2DA8C987-D5E9-4756-930E-1EBE4AC8D0AD}");
            var solutionNamedSubDir = solutionInfo.NestedSection.Dirs.Single(d => d.Name == "SolutionA");
            var innerSolutionItemsSubDir =
                solutionNamedSubDir.NestedProjects.Single(d => d.Project.Name == "Inner Solution Items");
            innerSolutionItemsSubDir.Project.ProjectInfo.All.Should().Contain("1.txt");
            solutionNamedSubDir.Name.Should().Be("SolutionA");
            solutionNamedSubDir.NestedProjects.Should().HaveCount(3);
            solutionNamedSubDir.NestedProjects.Should()
                .Contain(p => p.Project.Guid == "{8FE39D73-9DBF-47A0-94E3-24F96625B4EA}");
            solutionNamedSubDir.NestedProjects.Should()
                .Contain(p => p.Project.Guid == "{23030AF7-941A-498B-805B-2EF13D6982E7}");

            solutionInfo.SolutionPlatformsSection.Lines.Should().HaveCount(4);
            solutionInfo.SolutionPlatformsSection.Lines.Should()
                .Contain(p => p.Key == "Debug|Any CPU" && p.Value == "Debug|Any CPU");
            solutionInfo.SolutionPlatformsSection.Lines.Should()
                .Contain(p => p.Key == "Release|Any CPU" && p.Value == "Release|Any CPU");

            solutionInfo.ProjectPlatformsSection.Lines.Should().HaveCount(24);
            solutionInfo.ProjectPlatformsSection.Lines.Should()
                .Contain(p =>
                    p.Key == "{23030AF7-941A-498B-805B-2EF13D6982E7}.Debug|Any CPU.ActiveCfg" && p.Value == "Debug|Any CPU");
            solutionInfo.ProjectPlatformsSection.Lines.Should()
                .Contain(p =>
                    p.Key == "{177D7443-2536-4B05-8CBD-5FAD3CE75FD3}.Release|Any CPU.Build.0" && p.Value == "Release|Any CPU");
        }

        [Fact]
        // ReSharper disable once InconsistentNaming
        public void MergeSolutionBC_ShouldNotContainDuplicateProjects()
        {
            var outDir = Path.Combine(Path.GetTempPath(), nameof(MergeSolutionsTests));
            RecursiveDelete(new DirectoryInfo(outDir));
            Directory.CreateDirectory(outDir);
            const string outSolutionName = "out.sln";

            var solutionPaths = new[] {"TestData/SolutionB/SolutionB.sln", "TestData/SolutionC/SolutionC.sln"};
            var expectedProjectGuids = new[] {"{EADACA47-6660-4693-A6A8-6ACFF1CF6A46}", "{E77589E3-8B28-4BEC-B486-73720938DF07}"};
            var expectedConfigurations = new[] {"Debug|Any CPU", "LocalHost|Any CPU", "Production|Any CPU", "Release|Any CPU"};
            var expectedSolutionPlatforms = expectedConfigurations.ToDictionary(c => c, c => c);
            var expectedProjectPlatforms = expectedProjectGuids.SelectMany(p =>
                expectedConfigurations.SelectMany(c =>
                    new KeyValuePair<string, string>[] {new($"{p}.{c}.Build.0", c), new($"{p}.{c}.ActiveCfg", c)})).ToArray();

            var outputSolutionPath = Path.Combine(outDir, outSolutionName);
            var contents = new List<string>();
            for (var i = 0; i < 2; i++)
            {
                var mergedSolution = SolutionInfo.MergeSolutions(Path.GetFileNameWithoutExtension(outputSolutionPath),
                    Path.GetDirectoryName(outputSolutionPath) ?? "",
                    out _,
                    null,
                    null,
                    solutionPaths.Select(n => SolutionInfo.Parse(n)).ToArray());
                mergedSolution.Save();
                contents.Add(File.ReadAllText(outputSolutionPath));
            }

            //Require idempotence
            contents[1].Should().Be(contents[0]);

            var solutionInfo = SolutionInfo.Parse(outputSolutionPath);

            solutionInfo.Projects.Should().HaveCount(5);

            solutionInfo.ExtensibilityGlobalsSection.SolutionGuid.Should().Be("{0F9B2256-FEFF-4D23-BFD6-05B98F4DBDF0}");

            solutionInfo.NestedSection.Dirs
                .Should()
                .HaveCount(2)
                .And
                .ContainSingle(d => d.Guid == "{0F9B2256-FEFF-4D23-BFD6-05B98F4DBDF0}")
                .Which.NestedProjects
                .Should()
                .Satisfy(p => p.Project.Guid == expectedProjectGuids[0],
                    p => p.Project.Guid == "{8C9DBCF6-C2A9-4D55-BD48-AADF40240336}",
                    p => p.Project.Guid == "{A1A2656E-4DC0-42CF-A0C2-4D0B66CDDB9B}");
            solutionInfo.NestedSection.Dirs
                .Should()
                .ContainSingle(d => d.Guid == "{8C9DBCF6-C2A9-4D55-BD48-AADF40240336}")
                .Which.NestedProjects
                .Should()
                .Satisfy(p => p.Project.Guid == expectedProjectGuids[1]);
            solutionInfo.NestedSection.Dirs
                .Should()
                .ContainSingle(p => p.Guid == "{8C9DBCF6-C2A9-4D55-BD48-AADF40240336}")
                .Which.NestedProjects
                .Should()
                .Satisfy(p => p.Project.Guid == expectedProjectGuids[1]);
            solutionInfo.NestedSection.Dirs
                .Should()
                .ContainSingle(d => d.Guid == "{8C9DBCF6-C2A9-4D55-BD48-AADF40240336}")
                .Which.NestedProjects
                .Should()
                .Satisfy(p => p.Project.Guid == expectedProjectGuids[1]);
            solutionInfo.NestedSection.Dirs
                .Should()
                .ContainSingle(d => d.Name == "SolutionB")
                .Which.NestedProjects
                .Should().HaveCount(3)
                .And
                .Satisfy(p => p.Project.Name == "Inner Solution Items",
                    p => p.Project.Guid == "{8C9DBCF6-C2A9-4D55-BD48-AADF40240336}",
                    p => p.Project.Guid == expectedProjectGuids[0])
                .And
                .ContainSingle(p => p.Project.Name == "Inner Solution Items")
                .Which.Project.ProjectInfo.All
                .Should().Contain("2.txt");

            solutionInfo.SolutionPlatformsSection.Lines.Should()
                .BeEquivalentTo(expectedSolutionPlatforms, options => options.WithoutStrictOrdering());

            solutionInfo.ProjectPlatformsSection.Lines.Should()
                .BeEquivalentTo(expectedProjectPlatforms, options => options.WithoutStrictOrdering());
        }

        [Fact]
        // ReSharper disable once InconsistentNaming
        public void MergeSolutionsAB()
        {
            var outDir = Path.Combine(Path.GetTempPath(), nameof(MergeSolutionsTests));
            RecursiveDelete(new DirectoryInfo(outDir));
            Directory.CreateDirectory(outDir);
            const string outSolutionName = "out.sln";

            var solutionPaths = new[] {"TestData/SolutionA/SolutionA.sln", "TestData/SolutionB/SolutionB.sln"};

            var outputSolutionPath = Path.Combine(outDir, outSolutionName);
            var contents = new List<string>();
            for (var i = 0; i < 2; i++)
            {
                var mergedSolution = SolutionInfo.MergeSolutions(Path.GetFileNameWithoutExtension(outputSolutionPath),
                    Path.GetDirectoryName(outputSolutionPath) ?? "",
                    out _,
                    null,
                    null,
                    solutionPaths.Select(n => SolutionInfo.Parse(n)).ToArray());
                mergedSolution.Save();
                contents.Add(File.ReadAllText(outputSolutionPath));
            }

            //Require idempotence
            contents[1].Should().Be(contents[0]);

            var solutionInfo = SolutionInfo.Parse(outputSolutionPath);
            solutionInfo.ExtensibilityGlobalsSection.SolutionGuid.Should().Be("{C579A93F-B294-0413-2A28-15EABAC8DB0F}");

            solutionInfo.Projects.Should().HaveCount(11);
            solutionInfo.NestedSection.Dirs.Should().HaveCount(4);
            solutionInfo.NestedSection.Dirs.Should()
                .Contain(p => p.Guid == "{8FE39D73-9DBF-47A0-94E3-24F96625B4EA}");
            var inFolderDir = solutionInfo.NestedSection.Dirs.Single(d => d.Guid == "{8FE39D73-9DBF-47A0-94E3-24F96625B4EA}");
            inFolderDir.NestedProjects.Should()
                .Contain(p => p.Project.Guid == "{177D7443-2536-4B05-8CBD-5FAD3CE75FD3}");
            inFolderDir.NestedProjects.Should()
                .Contain(p => p.Project.Guid == "{2DA8C987-D5E9-4756-930E-1EBE4AC8D0AD}");
            var solutionANamedSubDir = solutionInfo.NestedSection.Dirs.Single(d => d.Name == "SolutionA");
            solutionANamedSubDir.NestedProjects.Should().HaveCount(3);
            var innerSolutionItemsSubDirA =
                solutionANamedSubDir.NestedProjects.Single(d => d.Project.Name == "Inner Solution Items");
            innerSolutionItemsSubDirA.Project.ProjectInfo.All.Should().Contain("1.txt");
            solutionANamedSubDir.NestedProjects.Should()
                .Contain(p => p.Project.Guid == "{8FE39D73-9DBF-47A0-94E3-24F96625B4EA}");
            solutionANamedSubDir.NestedProjects.Should()
                .Contain(p => p.Project.Guid == "{23030AF7-941A-498B-805B-2EF13D6982E7}");
            var solutionBNamedSubDir = solutionInfo.NestedSection.Dirs.Single(d => d.Name == "SolutionB");
            solutionBNamedSubDir.NestedProjects.Should().HaveCount(3);
            var innerSolutionItemsSubDirB =
                solutionBNamedSubDir.NestedProjects.Single(d => d.Project.Name == "Inner Solution Items");
            innerSolutionItemsSubDirB.Project.ProjectInfo.All.Should().Contain("2.txt");
            solutionBNamedSubDir.NestedProjects.Should()
                .Contain(p => p.Project.Guid == "{8C9DBCF6-C2A9-4D55-BD48-AADF40240336}");
            solutionBNamedSubDir.NestedProjects.Should()
                .Contain(p => p.Project.Guid == "{EADACA47-6660-4693-A6A8-6ACFF1CF6A46}");

            solutionInfo.SolutionPlatformsSection.Lines.Should().HaveCount(5);
            solutionInfo.SolutionPlatformsSection.Lines.Should()
                .Contain(p => p.Key == "Debug|Any CPU" && p.Value == "Debug|Any CPU");
            solutionInfo.SolutionPlatformsSection.Lines.Should()
                .Contain(p => p.Key == "LocalHost|Any CPU" && p.Value == "LocalHost|Any CPU");
            solutionInfo.SolutionPlatformsSection.Lines.Should()
                .Contain(p => p.Key == "Release|Any CPU" && p.Value == "Release|Any CPU");

            solutionInfo.ProjectPlatformsSection.Lines.Should().HaveCount(46);
            solutionInfo.ProjectPlatformsSection.Lines.Should()
                .Contain(p =>
                    p.Key == "{23030AF7-941A-498B-805B-2EF13D6982E7}.Debug|Any CPU.ActiveCfg" && p.Value == "Debug|Any CPU");
            solutionInfo.ProjectPlatformsSection.Lines.Should()
                .Contain(p =>
                    p.Key == "{177D7443-2536-4B05-8CBD-5FAD3CE75FD3}.Release|Any CPU.Build.0" && p.Value == "Release|Any CPU");
            solutionInfo.ProjectPlatformsSection.Lines.Should()
                .Contain(p =>
                    p.Key == "{EADACA47-6660-4693-A6A8-6ACFF1CF6A46}.Debug|Any CPU.ActiveCfg" && p.Value == "Debug|Any CPU");
            solutionInfo.ProjectPlatformsSection.Lines.Should()
                .Contain(p =>
                    p.Key == "{E77589E3-8B28-4BEC-B486-73720938DF07}.Release|Any CPU.Build.0" && p.Value == "Release|Any CPU");
        }

        [Fact]
        public void NestedProjectsParsing()
        {
            var solutionInfo = SolutionInfo.Parse("TestData/SolutionA/SolutionA.sln");
            solutionInfo.NestedSection.Dirs.Should().HaveCount(2);
            solutionInfo.NestedSection.Dirs[0].Guid.Should().Be("{8FE39D73-9DBF-47A0-94E3-24F96625B4EA}");
            solutionInfo.NestedSection.Dirs[1].Guid.Should().Be("{6AFAC7DD-F27B-4FF5-82AF-4269B15CFFDF}");
            solutionInfo.NestedSection.Dirs[0].NestedProjects.Should().HaveCount(2);
            solutionInfo.NestedSection.Dirs[0].NestedProjects.Should()
                .Contain(p => p.Project.Guid == "{177D7443-2536-4B05-8CBD-5FAD3CE75FD3}");
            solutionInfo.NestedSection.Dirs[0].NestedProjects.Should()
                .Contain(p => p.Project.Guid == "{2DA8C987-D5E9-4756-930E-1EBE4AC8D0AD}");
        }

        [Fact]
        public void ProjectFilter()
        {
            var outDir = Path.Combine(Path.GetTempPath(), nameof(MergeSolutionsTests));
            RecursiveDelete(new DirectoryInfo(outDir));
            Directory.CreateDirectory(outDir);
            const string outSolutionName = "out.sln";

            var solutionPaths = new[] {"TestData/SolutionA/SolutionA.sln", "TestData/SolutionB/SolutionB.sln"};

            var outputSolutionPath = Path.Combine(outDir, outSolutionName);

            var mergedSolution = SolutionInfo.MergeSolutions(Path.GetFileNameWithoutExtension(outputSolutionPath),
                Path.GetDirectoryName(outputSolutionPath) ?? "",
                out _,
                project => project.Name is not "InSolutionFolderClassLibraryB",
                null,
                solutionPaths.Select(n => SolutionInfo.Parse(n)).ToArray());
            mergedSolution.Save();

            var solutionInfo = SolutionInfo.Parse(outputSolutionPath);

            solutionInfo.Projects.OfType<Project>().Should().HaveCount(4);
            solutionInfo.Projects.Should().NotContain(p => p.Name == "InSolutionFolderClassLibraryB");
            solutionInfo.Projects.Should().Contain(p => p.Name == "InDiskFolderClassLibrary");

            mergedSolution = SolutionInfo.MergeSolutions(Path.GetFileNameWithoutExtension(outputSolutionPath),
                Path.GetDirectoryName(outputSolutionPath) ?? "",
                out _,
                project => project.SolutionName is not "SolutionA" || project.Name is not "InDiskFolderClassLibrary",
                null,
                solutionPaths.Select(n => SolutionInfo.Parse(n)).ToArray());
            mergedSolution.Save();

            solutionInfo = SolutionInfo.Parse(outputSolutionPath);

            solutionInfo.Projects.OfType<Project>().Should().HaveCount(4);
            solutionInfo.Projects.Should().Contain(p => p.Name == "InSolutionFolderClassLibraryB");
            solutionInfo.Projects.Should().NotContain(p => p.Name == "InDiskFolderClassLibrary");
        }

        [Fact]
        public void ProjectPlatformsSectionParsing()
        {
            var solutionInfo = SolutionInfo.Parse("TestData/SolutionA/SolutionA.sln");
            solutionInfo.ProjectPlatformsSection.Lines.Should().HaveCount(24);
            solutionInfo.ProjectPlatformsSection.Lines.Should()
                .Contain(p =>
                    p.Key == "{23030AF7-941A-498B-805B-2EF13D6982E7}.Debug|Any CPU.ActiveCfg" && p.Value == "Debug|Any CPU");
            solutionInfo.ProjectPlatformsSection.Lines.Should()
                .Contain(p =>
                    p.Key == "{177D7443-2536-4B05-8CBD-5FAD3CE75FD3}.Release|Any CPU.Build.0" && p.Value == "Release|Any CPU");
        }

        [Fact]
        public void SolutionPlatformsSectionParsing()
        {
            var solutionInfo = SolutionInfo.Parse("TestData/SolutionA/SolutionA.sln");
            solutionInfo.SolutionPlatformsSection.Lines.Should().HaveCount(4);
            solutionInfo.SolutionPlatformsSection.Lines.Should()
                .Contain(p => p.Key == "Debug|Any CPU" && p.Value == "Debug|Any CPU");
            solutionInfo.SolutionPlatformsSection.Lines.Should()
                .Contain(p => p.Key == "Release|Any CPU" && p.Value == "Release|Any CPU");
        }

        private static void RecursiveDelete(DirectoryInfo baseDir)
        {
            if (!baseDir.Exists)
            {
                return;
            }

            foreach (var dir in baseDir.EnumerateDirectories())
            {
                RecursiveDelete(dir);
            }

            var files = baseDir.GetFiles();

            foreach (var file in files)
            {
                file.IsReadOnly = false;
                file.Delete();
            }

            baseDir.Delete();
        }
    }
}