// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Xunit;

namespace NuGet.Build.Tasks.Test
{
    public class StaticGraphRestoreArgumentsTests
    {
        /// <summary>
        /// Verifies that the <see cref="StaticGraphRestoreArguments.Write(TextWriter)" /> and <see cref="StaticGraphRestoreArguments.Read(TextReader)" /> support global properties with complex characters like line breaks, XML, and JSON.
        /// </summary>
        [Fact]
        public void Read_WhenGLobalPropertiesContainComplexCharacters_CanBeRead()
        {
            var expected = new StaticGraphRestoreArguments
            {
                GlobalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["PublishDir"] = @"D:\RPublish",
                    ["SolutionFileName"] = "isomething.sln",
                    ["LangName"] = "en-US",
                    ["PublishTrimmed"] = "false",
                    ["CurrentSolutionConfigurationContents"] = @"<SolutionConfiguration>
<ProjectConfiguration Project=""{10192869-f315-4bd4-af9b-2a20cb8a6afa}"" AbsolutePath=""D:\src\isomething\lib\Recon\isomething.Recon.BackProjectors\isomething.Recon.BackProjectors.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{e854317c-0c24-4927-beef-e2c3b2c2fc9f}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.CoolTools\isomething.CoolTools.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{93c9f765-c59e-48b6-8cf0-d315d9513312}"" AbsolutePath=""D:\src\isomething\lib\Core\isomething.Core\isomething.Core.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{08dbc592-69ce-40c1-a2e8-21cf76867148}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.CUDA\contoso.CUDA.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{26b08e60-e547-4a98-ab65-8299bda93ff3}"" AbsolutePath=""D:\src\isomething\lib\Dicom\isomething.Dicom\isomething.Dicom.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{8c1c440f-bc2c-46d9-bf74-2f54bb9d609c}"" AbsolutePath=""D:\src\isomething\lib\Display\contoso.Display\contoso.Display.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{85ee153f-498e-4ca8-bd3b-061c87161090}"" AbsolutePath=""D:\src\isomething\lib\Volume\isomething.Volume.Cuda\isomething.Volume.Cuda.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{3be8e78d-28ef-4a11-80da-9cd858afe817}"" AbsolutePath=""D:\src\isomething\lib\Volume\isomething.Volume\isomething.Volume.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{36cd0c36-fef1-4908-9dee-938eaff792e8}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawData.Prep\contoso.RawData.Prep.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{1d0f9c28-33fb-427d-8032-687b0ce7daa4}"" AbsolutePath=""D:\src\isomething\lib\Recon\isomething.Recon.Algorithms\isomething.Recon.Algorithms.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{2b224b5d-27e5-48ad-8bac-a6a11f41fef9}"" AbsolutePath=""D:\src\isomething\lib\Recon\isomething.Recon.ForwardProjectors\isomething.Recon.ForwardProjectors.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{07f7947d-b48d-4434-8336-aa2415ac8b9c}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawData\isomething.RawData.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{3ef8b454-fa2f-4636-9994-b6c239077511}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawData.Cuda\contoso.RawData.Cuda.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{62cb8494-0417-45b4-a33e-680732ddd8f2}"" AbsolutePath=""D:\src\isomething\lib\DataProcessing\isomething.Algebra\isomething.Core.Algebra.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{ef324cfb-9278-4653-95a1-5ae524f11db5}"" AbsolutePath=""D:\src\isomething\Apps\ReconCT\ReconCT\ReconCT.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{34d996f6-509c-492c-81d0-e4a2539b62e6}"" AbsolutePath=""D:\src\isomething\Apps\irunner\contoso.irunner\contoso.irunner.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{3ddc7c87-f293-4b43-b19d-c26274355d5f}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.dual\irunner.dual.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f5aaeb3d-c593-4e5c-9e2e-04644c1d0586}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.dual.Basic\irunner.dual.Basic.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{a04eeeb5-737a-4aba-9c13-fc086da026b9}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.dual.spoon\irunner.dual.spoon.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{e44ce740-de53-4723-930c-ad244c600b22}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.dual.mapper\irunner.dual.mapper.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{5ab26916-e2a5-47a7-859f-19d390df8141}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.dual.Foot\irunner.dual.Foot.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{a38cff51-ed42-4e05-b121-25b19ba63a40}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.dual.RhoZ\irunner.dual.RhoZ.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{40191807-624d-4758-9ed0-ebe78542e6d6}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.dual.TumorSegmentation\irunner.dual.TumorSegmentation.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{9739473d-1fbd-4f6c-8a27-36c3024605d9}"" AbsolutePath=""D:\src\isomething\Apps\irunner\irunner\irunner.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{5c85ab2c-d3aa-42aa-80ce-f1632d573f13}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Segmentation\isomething.ImageProcessing.Algorithms.Segmentation.Coronary\isomething.ImageProcessing.Algorithms.Segmentation.Coronary.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{49ae5316-1266-4526-92ac-fd68e4984159}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.BestContrast\irunner.BestContrast.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{a0720040-12ac-4d3f-b2ea-f269303d70ab}"" AbsolutePath=""D:\src\isomething\Apps\ViSi\ViSi.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{6f5cbb85-ae2b-4224-b817-037507981338}"" AbsolutePath=""D:\src\isomething\Apps\ReconCT\contoso.ScannerControls\contoso.ScannerControls.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{6542cbec-d9e2-413f-88d7-f3afb20ed72c}"" AbsolutePath=""D:\src\isomething\Apps\ReconCT\contoso.ReconCT\contoso.ReconCT.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{6da60955-ce9b-4fcd-8369-278e5feac787}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.dual.IronVnc\irunner.dual.IronVnc.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{20a23c05-592e-4844-81d5-5e112a41a477}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.CT.VirtualXRay\irunner.CT.VirtualXRay.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{6334ec76-76f1-468c-92a2-65908d01e3b4}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.Application\contoso.Application.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{8aec95d6-4c5e-45e5-9da1-cb48a7cd00e8}"" AbsolutePath=""D:\src\isomething\lib\ImageDatabase\isomething.ImageDatabase.Volume\isomething.ImageDatabase.Volume.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{9238c3f9-a37a-45bc-97c0-dd4d57953071}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.dual.SPR\irunner.dual.SPR.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{7f65cdd4-cbe0-4ce1-a4d6-fdd859b1d6fb}"" AbsolutePath=""D:\src\isomething\lib\SignalProcessing\isomething.SignalProcessing\isomething.SignalProcessing.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f9334c74-c153-43d5-9e5a-175a852f5c19}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.Dose\isomething.Dose.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{4411f52f-c83a-4fdf-b29c-dba46f60fa85}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.FootVentilation\irunner.FootVentilation.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{4fd47cd4-862e-4d23-abb0-6b9b4ca91ad8}"" AbsolutePath=""D:\src\isomething\lib\Canvas\isomething.Canvas\isomething.Canvas.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f03e0f19-1963-45c4-ba76-3fd885cb1145}"" AbsolutePath=""D:\src\isomething\lib\Canvas\isomething.Canvas.Contracts\isomething.Canvas.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{b6497aec-a33d-4f8d-a7b2-1f18be8569e9}"" AbsolutePath=""D:\src\isomething\lib\Canvas\isomething.Canvas.Gdi\isomething.Canvas.View.Gdi.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{714a0c62-7480-47b8-84c3-bfc3e2be1b80}"" AbsolutePath=""D:\src\isomething\lib\Canvas\isomething.Canvas.View\isomething.Canvas.View.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{2db0c512-e4b1-4eda-a33b-7a1bec0041e5}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.Application.Devices\contoso.Application.Devices.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{c14678ee-a6b6-4b1e-8d80-59307c62136f}"" AbsolutePath=""D:\src\isomething\lib\Volume\isomething.Volume.IVT\isomething.Volume.IVT.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{44cce6ae-a517-44cc-ab7b-c47154428569}"" AbsolutePath=""D:\src\isomething\lib\SignalProcessing\isomething.SignalProcessing.Cuda\isomething.SignalProcessing.Cuda.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{236353d3-6256-44da-9d3c-fe81172a3f1b}"" AbsolutePath=""D:\src\isomething\Apps\ReconCT\contoso.ReconCT.Dicomization\contoso.ReconCT.Dicomization.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{239c8b47-28db-4287-92ff-757de0211cfd}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.dual.FootMono\irunner.dual.FootMono.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{2c891817-477f-40b3-8045-ef49701801b8}"" AbsolutePath=""D:\src\isomething\Apps\irunner\contoso.irunner.Contracts\contoso.irunner.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{b8b16daf-0d73-4309-924f-0759d38d6ddb}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.dual.Xmap\irunner.dual.Xmap.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{531ad9db-86a5-4808-9fb7-fa776e65099a}"" AbsolutePath=""D:\src\isomething\lib\DataProcessing\isomething.Algorithms\isomething.Core.Algorithms.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f72caff1-ccc9-4e2c-af63-3e69a5193248}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawData.Tables\contoso.RawData.Tables.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{14123b44-4ce0-4bbf-94d1-931142f01674}"" AbsolutePath=""D:\src\isomething\lib\Data\isomething.Data\isomething.Data.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{250e58e2-b0e1-41c9-8b74-b0deff5617e5}"" AbsolutePath=""D:\src\isomething\lib\Data\isomething.Data.UI\isomething.Data.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{538ee73f-6819-4d4a-b793-c2f669edd6fa}"" AbsolutePath=""D:\src\isomething\lib\Volume\isomething.Volume.Cached\isomething.Volume.Cached.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{8ddb4f8c-0c19-4778-8505-aaa13f479a7d}"" AbsolutePath=""D:\src\isomething\lib\Dicom\isomething.Dicom.Contracts\isomething.Dicom.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{b9f532e7-0764-4f83-8d35-04716cfb3ab0}"" AbsolutePath=""D:\src\isomething\lib\Visualization\isomething.Viewing.Contracts\isomething.Viewing.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{a47072b0-02da-4fa7-9fb5-c5e2e1e15e7b}"" AbsolutePath=""D:\src\isomething\lib\Volume\isomething.Volume.Contracts\isomething.Volume.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f62db07a-a4fe-4c3c-afc4-9c50f030fe3e}"" AbsolutePath=""D:\src\isomething\lib\Dicom\isomething.Dicom.UI\isomething.Dicom.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{438522bb-b4c2-4f75-927b-d6591d2a7b94}"" AbsolutePath=""D:\src\isomething\lib\Visualization\isomething.Viewing.UI\isomething.Viewing.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{90e2183a-61d3-4fee-8301-f676d33a3474}"" AbsolutePath=""D:\src\isomething\lib\Dicom\isomething.Dicom.Test\isomething.Dicom.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{1860d606-a71b-42e2-9e2f-bdbfbd2120a4}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\isomething.ImageProcessing.Test\contoso.ImageProcessing.Test.Unit.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{e2ddbcf6-d7f3-476c-9e77-7c9eb3dc540e}"" AbsolutePath=""D:\src\isomething\lib\SignalProcessing\isomething.SignalProcessing.Test\isomething.SignalProcessing.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f2ecfbf6-7358-410e-ad3f-f4c8561337dc}"" AbsolutePath=""D:\src\isomething\lib\Volume\isomething.Volume.Test\isomething.Volume.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{fc09353d-1728-4c26-a757-a2e726479274}"" AbsolutePath=""D:\src\isomething\lib\DataProcessing\isomething.CUDA.Test\isomething.CUDA.Tests.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f8953f70-ce7b-4f62-8bf4-1c8d7726db82}"" AbsolutePath=""D:\src\isomething\lib\Display\isomething.Display.Contracts\isomething.Display.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{9507008f-64c2-4026-88b5-ac4e04f5e287}"" AbsolutePath=""D:\src\isomething\lib\Visualization\isomething.Viewing\isomething.Viewing.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{3a8e14f9-d081-4c3d-a965-d5184b5e8002}"" AbsolutePath=""D:\src\isomething\lib\Simulation\isomething.Canvas.Simulation\isomething.Canvas.Simulation.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{b56caef8-4aef-49aa-9d67-b4cfd70a5a5e}"" AbsolutePath=""D:\src\isomething\lib\Simulation\isomething.Canvas.Simulation.Cuda\isomething.Canvas.Simulation.Cuda.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{79d7b5c4-ca5d-4f47-8097-873b626183b0}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.Automata\isomething.Automata.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{c24b230a-e02f-44e2-aa0a-18effbcc35da}"" AbsolutePath=""D:\src\isomething\lib\Simulation\isomething.Simulation.ModeLib.Test\isomething.Simulation.ModeLib.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{a7fccae2-624e-4a4a-b6b2-ad75e363c77e}"" AbsolutePath=""D:\src\isomething\lib\Data\isomething.Data.Contracts\isomething.Data.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{4c2a9e76-8278-43d6-b65e-4675f2835856}"" AbsolutePath=""D:\src\isomething\lib\Volume\isomething.Volume.Windows\isomething.Volume.Windows.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f725ec0b-3345-474b-aecb-0ea6768097a2}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.FourDAdvanced\irunner.FourDAdvanced.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{e2546251-bccf-449a-9e0b-170c2c2d97ed}"" AbsolutePath=""D:\src\isomething\lib\ImageDatabase\isomething.ImageDatabase.Client\isomething.ImageDatabase.Client.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{34b48a5f-4dae-43ca-991f-aedbdf06882e}"" AbsolutePath=""D:\src\isomething\lib\ImageDatabase\isomething.ImageDatabase.Client.UI\isomething.ImageDatabase.Client.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{d1e2cf30-2069-44b2-9bab-377580d16b15}"" AbsolutePath=""D:\src\isomething\lib\ImageDatabase\isomething.ImageDatabase\isomething.ImageDatabase.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{c49000d2-5e01-4128-8641-bf68c715d720}"" AbsolutePath=""D:\src\isomething\lib\Data\isomething.Data.Volume\isomething.Data.Volume.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f65be653-deda-4201-afc5-5237e3ded588}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.ImageProcessing.Algorithms\isomething.ImageProcessing.Algorithms.Basic.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{ca91d643-08cb-47d8-9ee8-5206e1dec52e}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.ImageProcessing.Algorithms.Cuda\isomething.ImageProcessing.Algorithms.Cuda.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{728b70de-e0ac-45ac-924c-13e1250118b7}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.ImageProcessing\isomething.ImageProcessing.Core.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{89c09b94-c149-4c04-b694-c2602a672a96}"" AbsolutePath=""D:\src\isomething\lib\DataProcessing\isomething.Algebra.Test\isomething.Core.Algebra.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{84082958-bfc0-442b-8a45-048aefd17576}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.ImageProcessing.Algorithms.UI\contoso.ImageProcessing.Algorithms.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{baddadd0-ceb4-4827-afbb-e6f47ef8a215}"" AbsolutePath=""D:\src\isomething\lib\Simulation\isomething.Simulation.Test\isomething.Simulation.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{79cfc34c-6b6d-4326-a198-2364566dd7fc}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Spectral\isomething.ImageProcessing.dual\isomething.ImageProcessing.Core.dual.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{301a8364-328d-45ca-8119-e22303c9292d}"" AbsolutePath=""D:\src\isomething\lib\Simulation\isomething.Simulation.Models\isomething.Simulation.Models.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{2c9466d0-7d10-4702-b191-c9101282139e}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.Automata.UI\isomething.Automata.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{fc22436c-8c3c-4295-a0f5-8c9a26bf46d3}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.Viewing\irunner.Viewing.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{fb154b07-27f1-493b-ab14-2be7fb7c2d5b}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.Viewing.UI\irunner.Viewing.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{5a57595f-5ec2-4bac-954d-24ecd93bc644}"" AbsolutePath=""D:\src\isomething\lib\Tests\contoso.Serialization.Test\isomething.Serialization.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{6264a894-de5a-4159-967b-b82bc8709759}"" AbsolutePath=""D:\src\isomething\lib\Recon\isomething.Recon.Algorithms.Test\isomething.Recon.Algorithms.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{89851475-0244-4320-969c-5d1f8e40b0dd}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawData.ScannerData\contoso.RawData.ScannerData.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{2625607c-505f-4963-bcad-5230b31397d4}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawData.Contracts\contoso.RawData.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{6b17f90f-7771-40ef-bb69-847e7122182b}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawData.Xacb\contoso.RawData.Xacb.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{4e5e29d1-d8d9-4499-b9f2-b48533b3933e}"" AbsolutePath=""D:\src\isomething\Apps\irunner\contoso.irunner.UI\contoso.irunner.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{6133fc8c-bd73-4493-9bd1-e4ff4ae838f6}"" AbsolutePath=""D:\src\isomething\lib\Plugins\SimCT\contoso.Simulation.MonteCarlo\isomething.Simulation.MonteCarlo.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{d99828f6-15d1-498b-8e5b-b00466cb437c}"" AbsolutePath=""D:\src\isomething\lib\Simulation\isomething.Simulation.Models.UI\isomething.Simulation.Models.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{d3fdeafe-c56e-4c99-921a-d8b3adf20138}"" AbsolutePath=""D:\src\isomething\lib\Simulation\isomething.Simulation\isomething.Simulation.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{28344c91-bd6d-486b-a279-35f89502b2f0}"" AbsolutePath=""D:\src\isomething\lib\Simulation\isomething.Simulation.ModeLib\isomething.Simulation.ModeLib.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{48c35055-0973-43c5-a3ff-7f6e1f07b60b}"" AbsolutePath=""D:\src\isomething\lib\Plugins\SimCT\contoso.Simulation.CTSim\isomething.Simulation.CTSim.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{25f473fc-88f0-43ad-883b-6be00bfda04b}"" AbsolutePath=""D:\src\isomething\lib\Plugins\SimCT\contoso.Simulation.CTSim.UI\isomething.Simulation.CTSim.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{5cb6dff7-7aa5-49b0-ad2d-44e4e42030e4}"" AbsolutePath=""D:\src\isomething\lib\Plugins\SimCT\contoso.Simulation.MonteCarlo.UI\isomething.Simulation.MonteCarlo.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{c7ac17e9-d932-4f71-b4d4-2dfa3f86ba3e}"" AbsolutePath=""D:\src\isomething\lib\Plugins\SimCT\contoso.Simulation.TubeLoadController\isomething.Simulation.TubeLoadController.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{1313dade-f026-4567-ace5-4cf0f2fa1e79}"" AbsolutePath=""D:\src\isomething\lib\Plugins\SimCT\contoso.Simulation.TubeLoadController.UI\isomething.Simulation.TubeLoadController.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{947772e5-c74e-4c7d-a495-5adbf71dd658}"" AbsolutePath=""D:\src\isomething\lib\Plugins\SimCT\contoso.Simulation.XCAT\isomething.Simulation.XCAT.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{2c336925-5b13-49c5-abc7-fb6e512fbfbd}"" AbsolutePath=""D:\src\isomething\lib\Plugins\SimCT\contoso.Simulation.XCAT.UI\isomething.Simulation.XCAT.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{94e46360-bcfa-4510-bf82-0736912190b7}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.TubeLoadController\isomething.TubeLoadController.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{ec16339b-a968-4886-b2f8-c462b16adb76}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawData.ScanProtocols\isomething.Rawdata.ScanProtocols.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{7e26b836-7647-4fd9-bba9-576bc780a15c}"" AbsolutePath=""D:\src\isomething\Apps\ReconCT\ReconCT.Shell\ReconCT.CommandLine.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{b0a4128d-19d6-47bf-8481-05b803b6537f}"" AbsolutePath=""D:\src\isomething\lib\Volume\isomething.Volume.Comparison\isomething.Volume.Comparison.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{a58059a8-f936-4b68-b03b-9ab54955d4bb}"" AbsolutePath=""D:\src\isomething\lib\Volume\isomething.Volume.Comparison.Test\isomething.Volume.Comparison.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{bcccbd61-abc5-4ced-866f-572d22aa8098}"" AbsolutePath=""D:\src\isomething\Apps\VolumeComparer\VolumeComparer.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{13cd6eed-eb67-43ff-aded-1cba6d177419}"" AbsolutePath=""D:\src\isomething\lib\Display\isomething.Display.RoiTool.Test\isomething.Display.RoiTool.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{5002896e-4919-499c-b6b9-e90a4a9d4b81}"" AbsolutePath=""D:\src\isomething\lib\Recon\isomething.Recon.IR\isomething.Recon.IR.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{ae2b68bb-1a28-4d77-af3f-5932a58260ec}"" AbsolutePath=""D:\src\isomething\lib\DataProcessing\isomething.Algebra.MatlabInterop\isomething.Core.Algebra.Interop.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{1fcae9db-f479-4dc1-8df3-bbecdb40fa24}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.Application.Telerik\contoso.Application.Telerik.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{c09edf5d-b889-4118-b6c1-dd1016d09b59}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Radiomics\isomething.ImageProcessing.Algorithms.Radiomics.PyRadiomics.Test\isomething.ImageProcessing.Algorithms.Radiomics.PyRadiomics.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{0b93f2f0-d840-4848-a565-220d5fb64260}"" AbsolutePath=""D:\src\isomething\lib\Core\isomething.Core.Licensing\isomething.Core.Licensing.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{449da3cb-3189-4bb6-9506-621b8111fb49}"" AbsolutePath=""D:\src\isomething\lib\Tests\contoso.Codec.JpegLossless.Test\isomething.Codec.JpegLossless.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{af443ecf-1f3f-4f60-aedc-f08b77f06e9a}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.Recon4D\contoso.ReconCT.Plugins.Recon4D.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{ef4623e2-60d5-4f79-947f-a62069c01e58}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.DeepLearning\contoso.ReconCT.Plugins.DeepLearning.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{40dcd45c-532a-458b-9be6-bd1aa7b13a49}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.PAMoCo\contoso.ReconCT.Plugins.PAMoCo.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{627fcbce-bddc-47f1-89f7-175690269c45}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.ImageProcessing\contoso.ReconCT.Plugins.ImageProcessing.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{ebcc00d4-494d-49e6-acce-d785866919ec}"" AbsolutePath=""D:\src\isomething\Apps\ReconCT\contoso.ReconCT.UI\contoso.ReconCT.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{47b6eb28-24f5-46d5-8218-ec756fba9e80}"" AbsolutePath=""D:\src\isomething\Apps\RawDB\RawDB\RawDB.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{b885952d-c36d-45a3-bbc0-c08a0c1e343b}"" AbsolutePath=""D:\src\isomething\Apps\RawDB\contoso.Rawdata.Database.UI\contoso.Rawdata.Database.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{ebc7a77d-f7ff-4243-88df-632aff31aa97}"" AbsolutePath=""D:\src\isomething\Apps\RawDB\contoso.Rawdata.Database\contoso.Rawdata.Database.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{503773de-2fbb-4ba8-bd7c-10ac2c47f421}"" AbsolutePath=""D:\src\isomething\lib\ImageDatabase\isomething.ImageDatabase.Contracts\isomething.ImageDatabase.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{2d9dd28f-6e44-46f4-9d75-7337f65ccd0d}"" AbsolutePath=""D:\src\isomething\lib\ImageDatabase\isomething.ImageDatabase.Test\isomething.ImageDatabase.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{fa862b5b-d9d7-4ec6-af1f-ead1ecb47680}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawData.Prep.Config\contoso.RawData.Prep.Config.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{a7067edd-96c1-4741-9e71-dea3c20a5d76}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawData.Prep.Config.UI\contoso.RawData.Prep.Config.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{39df252e-83fb-4e98-ac3d-25396f4317b4}"" AbsolutePath=""D:\src\isomething\Apps\RawDataAnonymizer\RawDataAnonymizer.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{591bb462-0e3d-418d-a10e-1bd300e54c98}"" AbsolutePath=""D:\src\isomething\lib\RawData\isomething.RawData.Readers.Test\isomething.RawData.Readers.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{8e4aae6a-f518-4c28-ba59-527d0725dd32}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawData.Prep.Test\contoso.RawData.Prep.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{87fdb738-af01-41cb-9705-b33b5944042f}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawData.Prep.Samples\contoso.RawData.Prep.Samples.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f32b4889-5c06-427e-aa17-a8aee2ffd393}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawData.Prep.Common\contoso.RawData.Prep.Common.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{7c0abbb1-a6d6-4e30-adfa-cecad08109d9}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Texture\isomething.ImageProcessing.Algorithms.Texture.Admire\isomething.ImageProcessing.Algorithms.Texture.Admire.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{8cb2f502-40b0-4290-a41c-dbeec270b7ca}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Texture\isomething.ImageProcessing.Algorithms.Texture.Admire.Test\isomething.ImageProcessing.Algorithms.Texture.Admire.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{a4dd1bb5-9b2b-49a2-872d-9330eaef1654}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Spectral\isomething.ImageProcessing.Algorithms.dual.UI\isomething.ImageProcessing.Algorithms.dual.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{a4cb2950-7ada-4ec1-ae99-d01f5b7a1d1c}"" AbsolutePath=""D:\src\isomething\lib\Core\isomething.Core.ObjectMapping\isomething.Core.ObjectMapping.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f73681db-cc33-4968-81a3-ba14e9a67ae4}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\isomething.ImageProcessing.Algorithms.Cuda.Test\isomething.ImageProcessing.Algorithms.Cuda.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{ccf9d707-70bb-4c3a-a5dc-cf79f5a3aeba}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Spectral\isomething.ImageProcessing.Algorithms.MultiEnergy\isomething.ImageProcessing.Core.MultiEnergy.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{7c67fdf5-8c79-4ce9-a66d-cf8c201b9607}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Spectral\isomething.ImageProcessing.Algorithms.MultiEnergy.Decorrelation\isomething.ImageProcessing.Algorithms.MultiEnergy.Decorrelation.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{ca5c6c00-7f44-4dec-97e5-ef6584f69e94}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Spectral\isomething.ImageProcessing.Algorithms.MultiEnergy.MaterialDecomposition\isomething.ImageProcessing.Algorithms.MultiEnergy.MaterialDecomposition.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{a5208ceb-2e73-4ebd-bbce-cfcd3c988b71}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Spectral\isomething.ImageProcessing.Algorithms.MultiEnergy.Test\isomething.ImageProcessing.Algorithms.MultiEnergy.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{86ce5979-d3be-4078-868c-fa5bab9d1a26}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Spectral\isomething.ImageProcessing.Algorithms.MultiEnergy.SpectralMixing\isomething.ImageProcessing.Algorithms.MultiEnergy.SpectralMixing.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{61ff9b74-aea0-4f9a-b690-f11d5fae08e0}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\contoso.ImageProcessing.AnatomyDetectionProcess\contoso.ImageProcessing.AnatomyDetection.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{12e528cb-1121-485e-bbef-5f4871d33c55}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\isomething.ImageProcessing.AnatomyDetection.Test\contoso.ImageProcessing.AnatomyDetection.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{306edf4c-7f7e-464a-aefa-a98d4bfe68b3}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Segmentation\isomething.ImageProcessing.Algorithms.Segmentation.Tumor\isomething.ImageProcessing.Algorithms.Segmentation.Tumor.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{b849c9ab-c847-439a-a533-124487e9ca7b}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Segmentation\isomething.ImageProcessing.Algorithms.Segmentation.Foot.AI\isomething.ImageProcessing.Algorithms.Segmentation.Foot.AI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{ac4086a1-cb9a-4b0b-be64-c13a7ab2404d}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.MultiEnergy.MaterialDecomposition\irunner.MultiEnergy.MaterialDecomposition.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f3c1fcc1-bd9c-477f-9b48-d3e2e5f14a10}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\contoso.ImageProcessing.Algorithms.Foot\contoso.ImageProcessing.Algorithms.Ventilation.Foot.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{d69d6ddf-895c-43f3-a74d-ece62f039993}"" AbsolutePath=""D:\src\isomething\lib\DataProcessing\isomething.Algorithms.Test\isomething.Core.Algorithms.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{3b517385-5870-4a42-b636-1104961c8f65}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Segmentation\isomething.ImageProcessing.Algorithms.Segmentation.Foot.Classic\isomething.ImageProcessing.Algorithms.Segmentation.Foot.Classic.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{b4780b1a-dfb0-47c5-8ff4-7694ef01c694}"" AbsolutePath=""D:\src\isomething\lib\RawData\contoso.RawDataUtils.Test\isomething.Rawdata.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{73e8c437-5729-4f14-8eee-f82487dbb3a7}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.Plugins\isomething.Plugins.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{bd776079-8072-4e42-9178-47df03abb5a0}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.Spectral\contoso.ReconCT.Plugins.Spectral.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{020c877e-a68f-43d3-a5be-56d31aef05d5}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Spectral\isomething.ImageProcessing.Algorithms.dual\isomething.ImageProcessing.Algorithms.dual.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{5943387e-d93b-4ca5-8fee-c85b08fb7788}"" AbsolutePath=""D:\src\isomething\lib\Volume\isomething.Volume.Interop\isomething.Volume.Interop.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{d805ba4d-5116-4ff4-bcfb-72cb31154911}"" AbsolutePath=""D:\src\isomething\lib\Plugins\SimCT\contoso.Simulation.DirectSim\isomething.Simulation.DirectSim.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{1950fde9-2609-4d75-a71f-d5cc473e8cc6}"" AbsolutePath=""D:\src\isomething\lib\Plugins\SimCT\contoso.Simulation.DirectSim.UI\isomething.Simulation.DirectSim.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{05da8546-79e4-4d3a-a835-fb6d329d1301}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.Spectral.UI\contoso.ReconCT.Plugins.Spectral.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{be0fe8df-38c4-44be-88e9-acadcb902031}"" AbsolutePath=""D:\src\isomething\lib\Tests\contoso.ReconCT.Plugins.Spectral.Demo\contoso.ReconCT.Plugins.Spectral.Demo.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{a6fa46be-91f4-4eae-abc6-685cf907d2a7}"" AbsolutePath=""D:\src\isomething\lib\Core.UI\isomething.Core.UI\isomething.Core.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{95e08dfc-ed3d-4121-bfd7-d5a1606dc8d5}"" AbsolutePath=""D:\src\isomething\lib\Managed\contoso.CSharp.Windows\isomething.Core.Windows.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{73600cd5-6cbe-4185-9b00-6450750b890c}"" AbsolutePath=""D:\src\isomething\lib\Core\isomething.Core.Algorithms.UI\isomething.Core.Algorithms.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{eaa428ae-57c5-4aaf-b311-4373a70bd82b}"" AbsolutePath=""D:\src\isomething\lib\Tests\contoso.ReconCT.Plugins.Spectral.Test\contoso.ReconCT.Plugins.Spectral.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{5e4a3621-bdff-4cea-9f1d-03d94b535986}"" AbsolutePath=""D:\src\isomething\lib\Volume\isomething.Volume.SPP\isomething.Volume.SPP.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{cab76a63-7b97-45f0-80e5-b8650a7d226e}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.NoiseCalibration\contoso.ReconCT.Plugins.NoiseCalibration.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{84da2b15-989d-46c0-b762-b766d520c6fe}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.NoiseCalibration.UI\contoso.ReconCT.Plugins.NoiseCalibration.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{77c630ef-1d23-45e7-8200-633b166b511f}"" AbsolutePath=""D:\src\isomething\lib\RawData\isomething.RawData.Math\isomething.RawData.Math.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{e6a60f35-ed3f-48c1-a238-17d855bbde22}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Radiomics\isomething.ImageProcessing.Algorithms.Radiomics.PyRadiomics\isomething.ImageProcessing.Algorithms.Radiomics.PyRadiomics.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{8d2baddf-c0c4-4bec-a639-8f73a7dd29e5}"" AbsolutePath=""D:\src\isomething\lib\Dicom\isomething.Dicom.SPP\isomething.Dicom.SPP.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{fe65a54e-7149-47d7-922c-1e3f3c4c68cd}"" AbsolutePath=""D:\src\isomething\lib\Dicom\isomething.Dicom.Communication\isomething.Dicom.Communication.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{51b0c34c-f547-4cf3-819c-13d24b91a260}"" AbsolutePath=""D:\src\isomething\Apps\ReconCT\isomething.Canvas.isomething\isomething.Canvas.isomething.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{c63f6b02-347e-4d97-a5c8-1961e14f37d2}"" AbsolutePath=""D:\src\isomething\lib\Plugins\SimCT\isomething.Simulation.DirectSim.Test\isomething.Simulation.DirectSim.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{2c999814-7fa6-42f7-bdb0-2b211827f979}"" AbsolutePath=""D:\src\isomething\lib\Recon\isomething.Recon\isomething.Recon.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{ac3c55da-efd9-427d-b42c-52adc3fd4fec}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Radiomics\isomething.ImageProcessing.Algorithms.Radiomics.PyRadiomics.UI\isomething.ImageProcessing.Algorithms.Radiomics.PyRadiomics.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{ef43fdc3-06c6-4d72-8edc-43bf2e75b0c6}"" AbsolutePath=""D:\src\isomething\lib\Core\isomething.Core.Test\isomething.Core.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{49c24ec6-15b5-447e-b188-36e9fa9b12cb}"" AbsolutePath=""D:\src\isomething\lib\ImageDatabase\isomething.ImageDatabase.Service\isomething.ImageDatabase.Service.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{fc250832-acef-445f-8d86-666a568f73a7}"" AbsolutePath=""D:\src\isomething\Apps\ImageDatabaseService\ImageDatabaseService.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{0aa2dbd5-386a-4d6e-aad2-1b3a429ad112}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\contoso.ImageProcessing.ImageJ\isomething.ImageProcessing.ImageJ.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{4b4b1a90-5a5a-410c-bd40-60f516b61b2e}"" AbsolutePath=""D:\src\isomething\lib\Display\contoso.Display.UI\contoso.Display.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f4412740-dfc8-41f6-aea6-8dc8b60998b5}"" AbsolutePath=""D:\src\isomething\lib\Display\contoso.Display.Tools.Cuda\contoso.Display.Tools.Cuda.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{8841295d-5f22-4470-9bc8-071fe2398443}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Segmentation\isomething.ImageProcessing.Algorithms.Segmentation.Liver\isomething.ImageProcessing.Algorithms.Segmentation.Liver.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{c2be809a-5a16-48fe-abf8-1d89b657fb0b}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Segmentation\isomething.ImageProcessing.Algorithms.Segmentation.Skull\isomething.ImageProcessing.Algorithms.Segmentation.Skull.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{26d427fc-c267-42a9-bb9b-18c160043257}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Registration\isomething.ImageProcessing.Algorithms.Registration\isomething.ImageProcessing.Algorithms.Registration.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{a6e93979-581a-4205-a96f-421aea74a929}"" AbsolutePath=""D:\src\isomething\Apps\DataTest\isomething.DataTest\isomething.DataTest.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{30a303f2-3fe5-4ee4-ae0f-e52e193f1fe3}"" AbsolutePath=""D:\src\isomething\Apps\DataTest\isomething.DataTestWrapper\isomething.DataTestWrapper.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{577a1482-8853-4cf3-9cc5-bf8885684a15}"" AbsolutePath=""D:\src\isomething\lib\MachineLearning\isomething.MachineLearning.Classification\isomething.MachineLearning.Classification.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{46057850-6f4d-48bc-8046-8cbe15dfc46c}"" AbsolutePath=""D:\src\isomething\lib\Recon\isomething.Recon.BackProjectors.Test.Unit.Core\isomething.Recon.BackProjectors.Test.Unit.Core.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{5ba057e5-2ef9-4bf3-aa37-1058eafd6eb3}"" AbsolutePath=""D:\src\isomething\lib\Dicom\isomething.Dicom.SPP.Test\isomething.Dicom.SPP.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{e3e0f5ab-6d25-4edc-804a-ef50f763a7f3}"" AbsolutePath=""D:\src\isomething\lib\Display\contoso.Display.Tools.UI\isomething.Display.Tools.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{5ee5c3c2-4dc7-4995-9784-519355df37f8}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Registration\isomething.ImageProcessing.Algorithms.Registration.Scr\isomething.ImageProcessing.Algorithms.Registration.Scr.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{b52c6001-b32a-4ef6-a16c-defc9a5aaeab}"" AbsolutePath=""D:\src\isomething\lib\Visualization\isomething.ProcessorGraph\isomething.ProcessorGraph.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{bf097aa0-a104-487e-aadb-d1363d70fa43}"" AbsolutePath=""D:\src\isomething\lib\Visualization\isomething.ProcessorGraph.UI\isomething.ProcessorGraph.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f20db750-d0b3-40ce-8514-9d26c5adc45b}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Registration\isomething.ImageProcessing.Algorithms.Registration.Test\isomething.ImageProcessing.Algorithms.Registration.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{c576cd69-4f26-403d-9272-0fb9ff210a6b}"" AbsolutePath=""D:\src\isomething\lib\Display\isomething.Display.RawData\isomething.Display.RawData.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{a9725ac1-2a7d-4e64-bcca-3a3d8ad14043}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.dual.FootMonoRemy\irunner.dual.FootMonoRemy.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{9ea7743f-622b-4e33-a64c-cd74bb81f9aa}"" AbsolutePath=""D:\src\isomething\lib\Network\isomething.Network.Notifications\isomething.Network.Notifications.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{7fa3977f-467e-482b-96d0-85f817980b68}"" AbsolutePath=""D:\src\isomething\lib\Network\isomething.Network.Notifications.Test\isomething.Network.Notifications.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{7807edfd-7393-4b9f-8253-b8fefa8a01bd}"" AbsolutePath=""D:\src\isomething\lib\Network\isomething.Network.TestHelpers\isomething.Network.TestHelpers.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{1fde1146-d775-4637-9c25-c9366f5d659b}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.StackCorrection\contoso.ReconCT.Plugins.StackCorrection.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{817b5691-3ebb-4e09-8f7c-55a1f8e80470}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.StackCorrection.Test\contoso.ReconCT.Plugins.StackCorrection.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{c0f7c38c-e5f1-421c-a81a-29e267658cd5}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Spectral\isomething.ImageProcessing.Algorithms.dual.Test\isomething.ImageProcessing.Algorithms.dual.Test.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{9e0ffeae-0cd9-41d9-b0b9-529b4ddd3735}"" AbsolutePath=""D:\src\isomething\lib\Core\isomething.Core.Algorithms.Contracts\isomething.Core.Algorithms.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{5ce4c29d-f1cd-44cd-940b-113273063134}"" AbsolutePath=""D:\src\isomething\lib\RawData\isomething.RawData.Cached\isomething.RawData.Cached.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{5e928ae5-f97c-41a5-af3a-ed653345c537}"" AbsolutePath=""D:\src\isomething\Apps\LifecycleManager\LifecycleManager\LifecycleManager.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{86a9c7f7-e44a-488e-bbf8-156aab189d73}"" AbsolutePath=""D:\src\isomething\Apps\LifecycleManager\isomething.LifecycleManagement\isomething.LifecycleManagement.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{49110443-6775-4156-832e-af199179e9c0}"" AbsolutePath=""D:\src\isomething\lib\Dev\isomething.Dev.VisualStudio.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{dabef40d-f1d6-4893-b051-cc04dfbf9d99}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\isomething.ImageProcessing.Algorithms.Contracts\isomething.ImageProcessing.Algorithms.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{28b21f63-7aab-40f6-8a17-b48aaad5d6a8}"" AbsolutePath=""D:\src\isomething\lib\Volume\.Benchmarks\isomething.Volume.Benchmarks.csproj"">Release|AnyCPU</ProjectConfiguration>
<ProjectConfiguration Project=""{6ac0b86e-c056-4fa9-8c6b-4798578a7438}"" AbsolutePath=""D:\src\isomething\lib\RawData\Tools\RawDataReadPerformance\RawDataReadPerformance.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{07a46655-40ff-4930-9662-4c8f69d4ad95}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.dual.MonoEnergeticProcessor\irunner.dual.MonoEnergetic.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{d1b26cf1-5b78-4415-a052-4267f691415f}"" AbsolutePath=""D:\src\isomething\Apps\DataTest\isomething.DataTest.Contracts\isomething.DataTest.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{8e6c7c03-22b4-487f-836c-92852e7715ff}"" AbsolutePath=""D:\src\isomething\lib\Display\contoso.Display.Tools\isomething.Display.Tools.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f6f5e136-35fd-4d0e-96bc-0bbf3f2d7a90}"" AbsolutePath=""D:\src\isomething\lib\SignalProcessing\isomething.SignalProcessing.Contracts\isomething.SignalProcessing.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{631ae3b1-d054-451f-8670-f9559ae312ed}"" AbsolutePath=""D:\src\isomething\lib\Plugins\irunner\irunner.QuantumBasic\irunner.QuantumBasic.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{53f2ae41-569a-49bf-aac0-30d6836847ce}"" AbsolutePath=""D:\src\isomething\Apps\ReconCT\isomething.ReconCT.Contracts\isomething.ReconCT.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{baab3b51-36df-4d61-b21c-2e0094d773fa}"" AbsolutePath=""D:\src\isomething\Apps\ReconCT\isomething.ReconCT.Recon\isomething.ReconCT.Recon.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{40c7fbde-493a-4579-8993-7a72346ede8e}"" AbsolutePath=""D:\src\isomething\Apps\ProcessorGraphDesigner\ProcessorGraphDesigner\ProcessorGraphDesigner.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{3d5a48d9-40a6-49be-82e7-e0736805f661}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.IRIS\contoso.ReconCT.Plugins.IRIS.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{063d37c4-eda6-49c2-b88b-0ee8ef1f6f32}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.IRIS.UI\contoso.ReconCT.Plugins.IRIS.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{dbac37b2-74c7-4eb6-beb5-e455c1bf144b}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.WindmillFilter\contoso.ReconCT.Plugins.WindmillFilter.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{392ea86f-3561-4083-b556-c9915a48cfa9}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.WindmillFilter.UI\contoso.ReconCT.Plugins.WindmillFilter.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{ab6258e0-abf1-4693-8af3-aaf8ee826450}"" AbsolutePath=""D:\src\isomething\Apps\DataTest\DataTestVisualizer\DataTestVisualizer.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{6c20dfa3-a9da-4db6-ad2f-22b2cf945909}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.PNR\isomething.ReconCT.Plugins.PNR.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{810d6ed4-2a8f-4287-a314-100a834592f4}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\contoso.ReconCT.Plugins.PNR.UI\isomething.ReconCT.Plugins.PNR.UI.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{e7db0f20-d93c-4d61-9fc9-a9d292a2cb4c}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\isomething.ReconCT.Plugins.IRIS.Config\isomething.ReconCT.Plugins.IRIS.Config.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{4542180d-5a6c-47c2-815a-82eb776d028a}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\isomething.ReconCT.Plugins.PNR.Config\isomething.ReconCT.Plugins.PNR.Config.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{43c51672-39dc-4f4f-9009-42ac82b9f52b}"" AbsolutePath=""D:\src\isomething\lib\Plugins\ReconCT\isomething.ReconCT.Plugins.WindmillFilter.Config\isomething.ReconCT.Plugins.WindmillFilter.Config.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{269c7d4e-026e-43f6-825c-4161148c1e89}"" AbsolutePath=""D:\src\isomething\lib\Recon\isomething.Recon.Contracts\isomething.Recon.Contracts.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{eb1632dc-6286-42eb-a50a-46ad508b1c25}"" AbsolutePath=""D:\src\isomething\Apps\ReconCT\isomething.ReconCT.Xacb\isomething.ReconCT.Xacb.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{e86bf4e2-19e5-499d-9c58-e73bbf5cdf99}"" AbsolutePath=""D:\src\isomething\lib\Prep\isomething.Prep.Pipeline\isomething.Prep.Pipeline.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{f93f4640-8094-4f5f-a0db-d8a61ce014f4}"" AbsolutePath=""D:\src\isomething\lib\Prep\isomething.Prep.Algorithms.ScatterCorrection\isomething.Prep.Algorithms.ScatterCorrection.csproj"">Release|x64</ProjectConfiguration>
<ProjectConfiguration Project=""{50448e09-f547-4c6a-9a30-addbc70f4b2d}"" AbsolutePath=""D:\src\isomething\lib\ImageProcessing\Registration\isomething.ImageProcessing.Algorithms.Registration.Scr.Test\isomething.ImageProcessing.Algorithms.Registration.Scr.Test.csproj"">Release|x64</ProjectConfiguration>
</SolutionConfiguration>",
                    ["Configuration"] = "Release",
                    ["RuntimeIdentifier"] = "win-x64",
                    ["LangID"] = "1033",
                    ["PublishProtocol"] = "FileSystem",
                    ["ProjectExtensionsPathForSpecifiedProject"] = @"D:\src\isomething\_Build\obj\RawDataAnonymizer\publish\win-x64\",
                    ["_TargetId"] = "Folder",
                    ["SolutionDir"] = @"D:\src\isomething\",
                    ["SolutionExt"] = ".sln",
                    ["BuildingInsideVisualStudio"] = "true",
                    ["EnableBaseIntermediateOutputPathMismatchWarning"] = "false",
                    ["UseHostCompilerIfAvailable"] = "false",
                    ["SelfContained"] = "true",
                    ["ProjectToOverrideProjectExtensionsPath"] = @"D:\src\isomething\Apps\RawDataAnonymizer\RawDataAnonymizer.csproj",
                    ["DefineExplicitDefaults"] = "true",
                    ["PublishReadyToRun"] = "false",
                    ["Platform"] = "x64",
                    ["SolutionPath"] = @"D:\src\isomething\isomething.sln",
                    ["SolutionName"] = "isomething",
                    ["VSIDEResolvedNonMSBuildProjectOutputs"] = @"<VSIDEResolvedNonMSBuildProjectOutputs />",
                    ["PublishSingleFile"] = "false",
                    ["DevEnvDir"] = @"C:\Program Files\Microsoft Visual Studio\2022\Preview\Common7\IDE\",
                    ["ExcludeRestorePackageImports"] = "true",
                    ["OriginalMSBuildStartupDirectory"] = @"D:\src\isomething"
                },
                Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [nameof(RestoreTaskEx.CleanupAssetsForUnsupportedProjects)] = bool.TrueString,
                    [nameof(RestoreTaskEx.Recursive)] = bool.TrueString,
                },
            };

            StringBuilder stringBuilder = new StringBuilder();

            using var writer = new StringWriter(stringBuilder);

            expected.Write(writer);

            using var reader = new StringReader(stringBuilder.ToString());

            StaticGraphRestoreArguments actual = StaticGraphRestoreArguments.Read(reader);

            actual.GlobalProperties.Should().BeEquivalentTo(expected.GlobalProperties);
            actual.Options.Should().BeEquivalentTo(expected.Options);
        }

        /// <summary>
        /// Verifies that the <see cref="StaticGraphRestoreArguments.Write(FileStream)" /> method writes a file in the expected JSON format.
        /// </summary>
        [Fact]
        public void Write_WithBasicInformation_WritesExpectedJson()
        {
            var arguments = new StaticGraphRestoreArguments
            {
                GlobalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Property1"] = "Value",
                },
                Options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    [nameof(RestoreTaskEx.CleanupAssetsForUnsupportedProjects)] = bool.TrueString,
                    [nameof(RestoreTaskEx.Recursive)] = bool.TrueString,
                },
            };

            StringBuilder stringBuilder = new StringBuilder();

            using var writer = new StringWriter(stringBuilder);

            arguments.Write(writer);

            string actual = stringBuilder.ToString();

            actual.Should().Be(@"{
  ""GlobalProperties"": {
    ""Property1"": ""Value""
  },
  ""Options"": {
    ""CleanupAssetsForUnsupportedProjects"": ""True"",
    ""Recursive"": ""True""
  }
}");
        }
    }
}
