﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;
using NuGet.Configuration;
using NuGet.Frameworks;
using NuGet.LibraryModel;
using NuGet.ProjectModel;
using NuGet.Protocol.Core.Types;
using NuGet.Test.Utility;
using NuGet.Versioning;
using Xunit;

namespace NuGet.Commands.Test
{
    public class ContentFilesMSBuildTests
    {
        [Fact]
        public async Task ContentFilesMSBuild_VerifyNoContentItemsForEmptyFolder()
        {
            // Arrange
            var logger = new TestLogger();

            using (var cacheContext = new SourceCacheContext())
            using (var pathContext = new SimpleTestPathContext())
            {
                var tfi = new List<TargetFrameworkInformation>
                {
                    new TargetFrameworkInformation()
                    {
                        FrameworkName = NuGetFramework.Parse("net462")
                    }
                };

                var spec = NETCoreRestoreTestUtility.GetProject(projectName: "projectA", framework: "net46");
                spec.Dependencies.Add(new LibraryDependency()
                {
                    LibraryRange = new LibraryRange("a", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package)
                });

                var project = NETCoreRestoreTestUtility.CreateProjectsFromSpecs(pathContext, spec).Single();

                var packageA = new SimpleTestPackageContext("a");
                packageA.AddFile("contentFiles/any/any/_._");
                packageA.AddFile("contentFiles/cs/net45/_._");
                packageA.AddFile("contentFiles/cs/any/_._");

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageA);

                // Create dg file
                var dgFile = new DependencyGraphSpec();
                dgFile.AddProject(spec);
                dgFile.AddRestore(spec.RestoreMetadata.ProjectUniqueName);

                dgFile.Save(Path.Combine(pathContext.WorkingDirectory, "out.dg"));

                // Act
                var result = (await NETCoreRestoreTestUtility.RunRestore(
                    pathContext,
                    logger,
                    new List<PackageSource>() { new PackageSource(pathContext.PackageSource) },
                    dgFile,
                    cacheContext)).Single();

                var props = XDocument.Load(project.PropsOutput);
                var itemGroups = props.Root.Elements(XName.Get("ItemGroup", "http://schemas.microsoft.com/developer/msbuild/2003")).ToArray();

                // Assert
                Assert.True(result.Success, logger.ShowErrors());
                Assert.Equal(0, itemGroups.Length);
            }
        }

        [Theory]
        [InlineData("contentFiles/any/any/x.txt", "'$(ExcludeRestorePackageImports)' != 'true'")]
        [InlineData("contentFiles/cs/any/x.txt", "'$(Language)' != 'any' AND '$(ExcludeRestorePackageImports)' != 'true'")]
        public async Task ContentFilesMSBuild_VerifyConditionForContentItemGroupWithoutCrossTargeting(string file, string expected)
        {
            // Arrange
            var logger = new TestLogger();

            using (var cacheContext = new SourceCacheContext())
            using (var pathContext = new SimpleTestPathContext())
            {
                var tfi = new List<TargetFrameworkInformation>
                {
                    new TargetFrameworkInformation()
                    {
                        FrameworkName = NuGetFramework.Parse("net462")
                    }
                };

                var spec = NETCoreRestoreTestUtility.GetProject(projectName: "projectA", framework: "net46");
                spec.Dependencies.Add(new LibraryDependency()
                {
                    LibraryRange = new LibraryRange("a", VersionRange.Parse("1.0.0"), LibraryDependencyTarget.Package)
                });

                var project = NETCoreRestoreTestUtility.CreateProjectsFromSpecs(pathContext, spec).Single();

                var packageA = new SimpleTestPackageContext("a");
                packageA.AddFile(file);

                SimpleTestPackageUtility.CreatePackages(pathContext.PackageSource, packageA);

                // Create dg file
                var dgFile = new DependencyGraphSpec();
                dgFile.AddProject(spec);
                dgFile.AddRestore(spec.RestoreMetadata.ProjectUniqueName);

                dgFile.Save(Path.Combine(pathContext.WorkingDirectory, "out.dg"));

                // Act
                var result = (await NETCoreRestoreTestUtility.RunRestore(
                    pathContext,
                    logger,
                    new List<PackageSource>() { new PackageSource(pathContext.PackageSource) },
                    dgFile,
                    cacheContext)).Single();

                var props = XDocument.Load(project.PropsOutput);
                var itemGroups = props.Root.Elements(XName.Get("ItemGroup", "http://schemas.microsoft.com/developer/msbuild/2003")).ToArray();

                // Assert
                Assert.True(result.Success, logger.ShowErrors());
                Assert.Equal(1, itemGroups.Length);
                Assert.EndsWith("x.txt", Path.GetFileName(itemGroups[0].Elements().Single().Attribute(XName.Get("Include")).Value));
                Assert.Equal(expected.Trim(), itemGroups[0].Attribute(XName.Get("Condition")).Value.Trim());
            }
        }
    }
}