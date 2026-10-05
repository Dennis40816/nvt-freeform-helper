using FreeformHelper.Application.Services;
using FreeformHelper.Domain.Geometry;
using FreeformHelper.Domain.Pads;
using Xunit;

namespace FreeformHelper.Tests;

public sealed class CoordinatePlannerComputationServiceTests
{
    [Fact]
    public void BuildSnapshot_WithAllArtifactKinds_PreservesCompleteSnapshot()
    {
        var request = BuildGuideModeRequest(CoordinateGuideGenerationMode.Count, CoordinateGuideGenerationMode.Count) with
        {
            MachineOriginX = 100d,
            MachineOriginY = -20d,
            MachineWidth = 80d,
            MachineHeight = 40d,
            PixelWidth = 800,
            PixelHeight = 200,
            HorizontalGuideCount = 2,
            VerticalGuideCount = 1,
            CopperPillarDiameter = 10d,
            ShowBistRectangle = true,
            ShowCustomArray = true,
            CustomArrayColumnCount = 3,
            CustomArrayRowCount = 2,
            CustomArrayTopLeftMachineX = 90d,
            CustomArrayTopLeftMachineY = -30d,
            CustomArrayTopRightMachineX = 154d,
            CustomArrayTopRightMachineY = 18d,
            CustomArrayBottomRightMachineX = 154d,
            CustomArrayBottomRightMachineY = 58d,
            CustomArrayBottomLeftMachineX = 90d,
            CustomArrayBottomLeftMachineY = 10d,
            CustomPoints =
            [
                new CoordinateCustomPointRequest(" ", "Ignored", 0d, 0d),
                new CoordinateCustomPointRequest("probe", "Probe A", 95d, 25d),
                new CoordinateCustomPointRequest("fallback", " ", 140d, 0d),
            ],
            CustomPaths =
            [
                new CoordinateCustomPathRequest(" ", "Ignored", 0d, 0d, 0d, 0d, 3),
                new CoordinateCustomPathRequest("sweep", "Edge sweep", 90d, -30d, 190d, 30d, 3),
                new CoordinateCustomPathRequest("single", " ", 140d, -30d, 140d, 30d, 1),
                new CoordinateCustomPathRequest("empty", "No steps", 120d, -10d, 160d, 10d, -1),
            ],
        };

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(
            new Rect2(10d, 20d, 90d, 60d), new Rect2(-8d, -4d, 8d, 4d), request);

        // Captured from the original service with round-trip coordinates and artifact order.
        const string expected = """
            AA=10,20,90,60|radius=5|worldRadius=5,5
            P|aa-tl|AA TL|AaCorner|world=10,60|safeWorld=15,55|pixel=0,0|machine=100,-20|safeMachine=105,-15
            P|aa-tr|AA TR|AaCorner|world=90,60|safeWorld=85,55|pixel=800,0|machine=180,-20|safeMachine=175,-15
            P|aa-br|AA BR|AaCorner|world=90,20|safeWorld=85,25|pixel=800,200|machine=180,20|safeMachine=175,15
            P|aa-bl|AA BL|AaCorner|world=10,20|safeWorld=15,25|pixel=0,200|machine=100,20|safeMachine=105,15
            P|bist-tl|BIST TL|BistCorner|world=30,50|safeWorld=30,50|pixel=200,50|machine=120,-10|safeMachine=120,-10
            P|bist-tr|BIST TR|BistCorner|world=70,50|safeWorld=70,50|pixel=600,50|machine=160,-10|safeMachine=160,-10
            P|bist-br|BIST BR|BistCorner|world=70,30|safeWorld=70,30|pixel=600,150|machine=160,10|safeMachine=160,10
            P|bist-bl|BIST BL|BistCorner|world=30,30|safeWorld=30,30|pixel=200,150|machine=120,10|safeMachine=120,10
            P|array-tl|Array TL|CustomArrayCorner|world=10,60|safeWorld=15,55|pixel=-100,-50|machine=90,-30|safeMachine=105,-15
            P|array-tr|Array TR|CustomArrayCorner|world=64,22|safeWorld=60,25|pixel=540,190|machine=154,18|safeMachine=150,15
            P|array-br|Array BR|CustomArrayCorner|world=64,20|safeWorld=60,25|pixel=540,390|machine=154,58|safeMachine=150,15
            P|array-bl|Array BL|CustomArrayCorner|world=10,30|safeWorld=15,32|pixel=-100,150|machine=90,10|safeMachine=105,8
            P|array-dot-r1-c1|P1-1|CustomArrayDot|world=10,60|safeWorld=15,55|pixel=-100,-50|machine=90,-30|safeMachine=105,-15
            P|array-dot-r1-c2|P1-2|CustomArrayDot|world=32,46|safeWorld=32,41|pixel=220.00000000000003,70|machine=122,-6|safeMachine=122,-1
            P|array-dot-r1-c3|P1-3|CustomArrayDot|world=64,22|safeWorld=60,25|pixel=540,190|machine=154,18|safeMachine=150,15
            P|array-dot-r2-c1|P2-1|CustomArrayDot|world=10,30|safeWorld=15,32|pixel=-100,150|machine=90,10|safeMachine=105,8
            P|array-dot-r2-c2|P2-2|CustomArrayDot|world=32,20|safeWorld=32,25|pixel=220.00000000000003,270|machine=122,34|safeMachine=122,15
            P|array-dot-r2-c3|P2-3|CustomArrayDot|world=64,20|safeWorld=60,25|pixel=540,390|machine=154,58|safeMachine=150,15
            P|custom-point-probe|Probe A|CustomPoint|world=10,20|safeWorld=15,25|pixel=-50,225|machine=95,25|safeMachine=105,15
            P|custom-point-fallback|Custom point|CustomPoint|world=50,40|safeWorld=50,40|pixel=400,100|machine=140,0|safeMachine=140,0
            P|custom-path-sweep-step-1|Edge sweep P1|CustomPathStep|world=10,60|safeWorld=15,55|pixel=-100,-50|machine=90,-30|safeMachine=105,-15
            P|custom-path-sweep-step-2|Edge sweep P2|CustomPathStep|world=50,40|safeWorld=50,40|pixel=400,100|machine=140,0|safeMachine=140,0
            P|custom-path-sweep-step-3|Edge sweep P3|CustomPathStep|world=90,20|safeWorld=85,25|pixel=900,250|machine=190,30|safeMachine=175,15
            P|custom-path-single-step-1|Custom path P1|CustomPathStep|world=50,40|safeWorld=50,40|pixel=400,100|machine=140,0|safeMachine=140,0
            L|h-1|H1|HorizontalGuide|world=-8,4,8,4|safeWorld=-7,3,7,3|pixel=0,0,800,0|machine=100,-20,180,-20|safeMachine=105,-15,175,-15
            L|h-2|H2|HorizontalGuide|world=-8,-4,8,-4|safeWorld=-7,-3,7,-3|pixel=0,200,800,200|machine=100,20,180,20|safeMachine=105,15,175,15
            L|v-1|V1|VerticalGuide|world=-8,4,-8,-4|safeWorld=-7,3,-7,-3|pixel=0,0,0,200|machine=100,-20,100,20|safeMachine=105,-15,105,15
            L|array-top|Array top|CustomArrayEdge|world=10,60,64,22|safeWorld=15,55,60,25|pixel=-100,-50,540,190|machine=90,-30,154,18|safeMachine=105,-15,150,15
            L|array-right|Array right|CustomArrayEdge|world=64,22,64,20|safeWorld=60,25,60,25|pixel=540,190,540,390|machine=154,18,154,58|safeMachine=150,15,150,15
            L|array-bottom|Array bottom|CustomArrayEdge|world=10,30,64,20|safeWorld=15,32,60,25|pixel=-100,150,540,390|machine=90,10,154,58|safeMachine=105,8,150,15
            L|array-left|Array left|CustomArrayEdge|world=10,60,10,30|safeWorld=15,55,15,32|pixel=-100,-50,-100,150|machine=90,-30,90,10|safeMachine=105,-15,105,8
            L|custom-path-sweep|Edge sweep|CustomPath|world=10,60,90,20|safeWorld=15,55,85,25|pixel=-100,-50,900,250|machine=90,-30,190,30|safeMachine=105,-15,175,15
            L|custom-path-single|Custom path|CustomPath|world=50,60,50,20|safeWorld=50,55,50,25|pixel=400,-50,400,250|machine=140,-30,140,30|safeMachine=140,-15,140,15
            L|custom-path-empty|No steps|CustomPath|world=30,50,70,30|safeWorld=30,50,70,30|pixel=200,50,600,150|machine=120,-10,160,10|safeMachine=120,-10,160,10
            R|bist-center|BIST center blank rect|BistCenter|world=30,30,70,50|safeWorld=35,35,65,45|pixel=200,50,600,150|machine=120,-10,160,10|safeMachine=125,-5,155,5
            """;
        AssertSnapshot(expected, snapshot);
    }

    [Theory]
    [InlineData(60d)]
    [InlineData(120d)]
    public void BuildSnapshot_WhenSafeRectangleCollapses_PreservesCompleteSnapshot(double diameter)
    {
        var request = BuildGuideModeRequest(CoordinateGuideGenerationMode.Count, CoordinateGuideGenerationMode.Count) with
        {
            MachineOriginX = -10d,
            MachineOriginY = 30d,
            MachineWidth = 80d,
            MachineHeight = 40d,
            PixelWidth = 800,
            PixelHeight = 200,
            HorizontalGuideCount = 1,
            VerticalGuideCount = 1,
            CopperPillarDiameter = diameter,
            ShowBistRectangle = true,
        };

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(new Rect2(10d, 20d, 90d, 60d), request);

        // The rectangle collapses before the point clamp does when diameter is 60.
        var expected = diameter == 60d
            ? """
            AA=10,20,90,60|radius=30|worldRadius=30,30
            P|aa-tl|AA TL|AaCorner|world=10,60|safeWorld=40,40|pixel=0,0|machine=-10,30|safeMachine=20,50
            P|aa-tr|AA TR|AaCorner|world=90,60|safeWorld=60,40|pixel=800,0|machine=70,30|safeMachine=40,50
            P|aa-br|AA BR|AaCorner|world=90,20|safeWorld=60,40|pixel=800,200|machine=70,70|safeMachine=40,50
            P|aa-bl|AA BL|AaCorner|world=10,20|safeWorld=40,40|pixel=0,200|machine=-10,70|safeMachine=20,50
            P|bist-tl|BIST TL|BistCorner|world=30,50|safeWorld=40,40|pixel=200,50|machine=10,40|safeMachine=20,50
            P|bist-tr|BIST TR|BistCorner|world=70,50|safeWorld=60,40|pixel=600,50|machine=50,40|safeMachine=40,50
            P|bist-br|BIST BR|BistCorner|world=70,30|safeWorld=60,40|pixel=600,150|machine=50,60|safeMachine=40,50
            P|bist-bl|BIST BL|BistCorner|world=30,30|safeWorld=40,40|pixel=200,150|machine=10,60|safeMachine=20,50
            L|h-1|H1|HorizontalGuide|world=10,60,90,60|safeWorld=40,40,60,40|pixel=0,0,800,0|machine=-10,30,70,30|safeMachine=20,50,40,50
            L|v-1|V1|VerticalGuide|world=10,60,10,20|safeWorld=40,40,40,40|pixel=0,0,0,200|machine=-10,30,-10,70|safeMachine=20,50,20,50
            R|bist-center|BIST center blank rect|BistCenter|world=30,30,70,50|safeWorld=50,40,50,40|pixel=200,50,600,150|machine=10,40,50,60|safeMachine=30,50,30,50
            """
            : """
            AA=10,20,90,60|radius=60|worldRadius=60,60
            P|aa-tl|AA TL|AaCorner|world=10,60|safeWorld=50,40|pixel=0,0|machine=-10,30|safeMachine=30,50
            P|aa-tr|AA TR|AaCorner|world=90,60|safeWorld=50,40|pixel=800,0|machine=70,30|safeMachine=30,50
            P|aa-br|AA BR|AaCorner|world=90,20|safeWorld=50,40|pixel=800,200|machine=70,70|safeMachine=30,50
            P|aa-bl|AA BL|AaCorner|world=10,20|safeWorld=50,40|pixel=0,200|machine=-10,70|safeMachine=30,50
            P|bist-tl|BIST TL|BistCorner|world=30,50|safeWorld=50,40|pixel=200,50|machine=10,40|safeMachine=30,50
            P|bist-tr|BIST TR|BistCorner|world=70,50|safeWorld=50,40|pixel=600,50|machine=50,40|safeMachine=30,50
            P|bist-br|BIST BR|BistCorner|world=70,30|safeWorld=50,40|pixel=600,150|machine=50,60|safeMachine=30,50
            P|bist-bl|BIST BL|BistCorner|world=30,30|safeWorld=50,40|pixel=200,150|machine=10,60|safeMachine=30,50
            L|h-1|H1|HorizontalGuide|world=10,60,90,60|safeWorld=50,40,50,40|pixel=0,0,800,0|machine=-10,30,70,30|safeMachine=30,50,30,50
            L|v-1|V1|VerticalGuide|world=10,60,10,20|safeWorld=50,40,50,40|pixel=0,0,0,200|machine=-10,30,-10,70|safeMachine=30,50,30,50
            R|bist-center|BIST center blank rect|BistCenter|world=30,30,70,50|safeWorld=50,40,50,40|pixel=200,50,600,150|machine=10,40,50,60|safeMachine=30,50,30,50
            """;
        AssertSnapshot(expected, snapshot);
    }

    [Fact]
    public void BuildSnapshot_WithZeroAndNegativeMachineSpans_PreservesCompleteSnapshot()
    {
        var request = BuildGuideModeRequest(CoordinateGuideGenerationMode.Count, CoordinateGuideGenerationMode.Count) with
        {
            MachineOriginX = 10d,
            MachineOriginY = 20d,
            MachineWidth = 0d,
            MachineHeight = -8d,
            PixelWidth = 0,
            PixelHeight = -4,
            HorizontalGuideCount = 2,
            VerticalGuideCount = 1,
            CopperPillarDiameter = 6d,
            ShowBistRectangle = true,
            CustomPoints = [new CoordinateCustomPointRequest("probe", "Probe A", 12d, 30d)],
            CustomPaths = [new CoordinateCustomPathRequest("single", "Single step", 8d, 10d, 20d, 40d, 1)],
        };

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(new Rect2(10d, 20d, 90d, 60d), request);

        // Captured from the original service with round-trip coordinates and artifact order.
        const string expected = """
            AA=10,20,90,60|radius=3|worldRadius=240000000000,120000000000
            P|aa-tl|AA TL|AaCorner|world=10,60|safeWorld=10,60|pixel=0,0|machine=10,20|safeMachine=10,16
            P|aa-tr|AA TR|AaCorner|world=90,60|safeWorld=10,60|pixel=1,0|machine=10,20|safeMachine=10,16
            P|aa-br|AA BR|AaCorner|world=90,20|safeWorld=10,60|pixel=1,1|machine=10,12|safeMachine=10,16
            P|aa-bl|AA BL|AaCorner|world=10,20|safeWorld=10,60|pixel=0,1|machine=10,12|safeMachine=10,16
            P|bist-tl|BIST TL|BistCorner|world=30,50|safeWorld=10,60|pixel=0.25,0.25|machine=10,18|safeMachine=10,16
            P|bist-tr|BIST TR|BistCorner|world=70,50|safeWorld=10,60|pixel=0.75,0.25|machine=10,18|safeMachine=10,16
            P|bist-br|BIST BR|BistCorner|world=70,30|safeWorld=10,60|pixel=0.75,0.75|machine=10,14|safeMachine=10,16
            P|bist-bl|BIST BL|BistCorner|world=30,30|safeWorld=10,60|pixel=0.25,0.75|machine=10,14|safeMachine=10,16
            P|custom-point-probe|Probe A|CustomPoint|world=10,60|safeWorld=10,60|pixel=0,0|machine=12,30|safeMachine=10,16
            P|custom-path-single-step-1|Single step P1|CustomPathStep|world=10,60|safeWorld=10,60|pixel=0,0|machine=14,25|safeMachine=10,16
            L|h-1|H1|HorizontalGuide|world=10,60,90,60|safeWorld=10,60,10,60|pixel=0,0,1,0|machine=10,20,10,20|safeMachine=10,16,10,16
            L|h-2|H2|HorizontalGuide|world=10,20,90,20|safeWorld=10,60,10,60|pixel=0,1,1,1|machine=10,12,10,12|safeMachine=10,16,10,16
            L|v-1|V1|VerticalGuide|world=10,60,10,20|safeWorld=10,60,10,60|pixel=0,0,0,1|machine=10,20,10,12|safeMachine=10,16,10,16
            L|custom-path-single|Single step|CustomPath|world=10,60,10,60|safeWorld=10,60,10,60|pixel=0,0,0,0|machine=8,10,20,40|safeMachine=10,16,10,16
            R|bist-center|BIST center blank rect|BistCenter|world=30,30,70,50|safeWorld=10,60,10,60|pixel=0.25,0.25,0.75,0.75|machine=10,18,10,14|safeMachine=10,16,10,16
            """;
        AssertSnapshot(expected, snapshot);
    }

    [Fact]
    public void BuildSnapshot_AtMachineSpanThreshold_PreservesCompleteSnapshot()
    {
        var request = BuildGuideModeRequest(CoordinateGuideGenerationMode.Count, CoordinateGuideGenerationMode.Count) with
        {
            MachineWidth = 1e-9,
            MachineHeight = 1.000000001e-9,
            PixelWidth = -2,
            PixelHeight = 0,
            HorizontalGuideCount = 2,
            VerticalGuideCount = 1,
            ShowBistRectangle = true,
            CustomPoints = [new CoordinateCustomPointRequest("probe", "Probe A", 2e-9, -1e-9)],
            CustomPaths = [new CoordinateCustomPathRequest("single", "Single step", -1e-9, 2e-9, 2e-9, -1e-9, 1)],
        };

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(new Rect2(10d, 20d, 90d, 60d), request);

        // Captured from the original service with round-trip coordinates and artifact order.
        const string expected = """
            AA=10,20,90,60|radius=0|worldRadius=0,0
            P|aa-tl|AA TL|AaCorner|world=10,60|safeWorld=10,60|pixel=0,0|machine=0,0|safeMachine=0,0
            P|aa-tr|AA TR|AaCorner|world=90,60|safeWorld=10,60|pixel=1,0|machine=1E-09,0|safeMachine=1E-09,0
            P|aa-br|AA BR|AaCorner|world=90,20|safeWorld=10,20|pixel=1,1|machine=1E-09,1.000000001E-09|safeMachine=1E-09,1.000000001E-09
            P|aa-bl|AA BL|AaCorner|world=10,20|safeWorld=10,20|pixel=0,1|machine=0,1.000000001E-09|safeMachine=0,1.000000001E-09
            P|bist-tl|BIST TL|BistCorner|world=30,50|safeWorld=10,50|pixel=0.25,0.25|machine=2.5E-10,2.5000000025E-10|safeMachine=2.5E-10,2.5000000025E-10
            P|bist-tr|BIST TR|BistCorner|world=70,50|safeWorld=10,50|pixel=0.75,0.25|machine=7.500000000000001E-10,2.5000000025E-10|safeMachine=7.500000000000001E-10,2.5000000025E-10
            P|bist-br|BIST BR|BistCorner|world=70,30|safeWorld=10,30|pixel=0.75,0.75|machine=7.500000000000001E-10,7.5000000075E-10|safeMachine=7.500000000000001E-10,7.5000000075E-10
            P|bist-bl|BIST BL|BistCorner|world=30,30|safeWorld=10,30|pixel=0.25,0.75|machine=2.5E-10,7.5000000075E-10|safeMachine=2.5E-10,7.5000000075E-10
            P|custom-point-probe|Probe A|CustomPoint|world=10,60|safeWorld=10,60|pixel=0,-0.999999999|machine=2E-09,-1E-09|safeMachine=1E-09,0
            P|custom-path-single-step-1|Single step P1|CustomPathStep|world=10,40.00000002|safeWorld=10,40.00000002|pixel=0,0.4999999994999999|machine=5.000000000000001E-10,4.999999999999999E-10|safeMachine=5.000000000000001E-10,4.999999999999999E-10
            L|h-1|H1|HorizontalGuide|world=10,60,90,60|safeWorld=10,60,10,60|pixel=0,0,1,0|machine=0,0,1E-09,0|safeMachine=0,0,1E-09,0
            L|h-2|H2|HorizontalGuide|world=10,20,90,20|safeWorld=10,20,10,20|pixel=0,1,1,1|machine=0,1.000000001E-09,1E-09,1.000000001E-09|safeMachine=0,1.000000001E-09,1E-09,1.000000001E-09
            L|v-1|V1|VerticalGuide|world=10,60,10,20|safeWorld=10,60,10,20|pixel=0,0,0,1|machine=0,0,0,1.000000001E-09|safeMachine=0,0,0,1.000000001E-09
            L|custom-path-single|Single step|CustomPath|world=10,20,10,60|safeWorld=10,20,10,60|pixel=0,1.999999998,0,-0.999999999|machine=-1E-09,2E-09,2E-09,-1E-09|safeMachine=0,1.000000001E-09,1E-09,0
            R|bist-center|BIST center blank rect|BistCenter|world=30,30,70,50|safeWorld=10,30,10,50|pixel=0.25,0.25,0.75,0.75|machine=2.5E-10,2.5000000025E-10,7.500000000000001E-10,7.5000000075E-10|safeMachine=2.5E-10,2.5000000025E-10,7.500000000000001E-10,7.5000000075E-10
            """;
        AssertSnapshot(expected, snapshot);
    }

    [Fact]
    public void BuildSnapshot_ComputesAaCornersGuidesAndBistRectWithCopperSafeCoordinates()
    {
        var grid = BuildGrid(rows: 2, cols: 2, cellWidth: 10d, cellHeight: 5d);
        var request = new CoordinatePlannerRequest(
            MachineOriginX: 100d,
            MachineOriginY: 200d,
            MachineWidth: 200d,
            MachineHeight: 100d,
            PixelWidth: 1000,
            PixelHeight: 500,
            HorizontalGuideCount: 5,
            VerticalGuideCount: 5,
            CopperPillarDiameter: 10d,
            ShowBistRectangle: true,
            ShowCustomArray: false,
            CustomArrayColumnCount: 0,
            CustomArrayRowCount: 0,
            CustomArrayTopLeftMachineX: 100d,
            CustomArrayTopLeftMachineY: 200d,
            CustomArrayTopRightMachineX: 300d,
            CustomArrayTopRightMachineY: 200d,
            CustomArrayBottomRightMachineX: 300d,
            CustomArrayBottomRightMachineY: 300d,
            CustomArrayBottomLeftMachineX: 100d,
            CustomArrayBottomLeftMachineY: 300d);

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(grid, request);

        Assert.Equal(8, snapshot.Points.Count);
        Assert.Equal(10, snapshot.Lines.Count);
        Assert.Single(snapshot.Rectangles);
        Assert.Equal(5d, snapshot.CopperPillarRadiusMachine, 6);

        var aaTopLeft = Assert.Single(snapshot.Points, point => point.Key == "aa-tl");
        Assert.Equal(0d, aaTopLeft.PixelX, 6);
        Assert.Equal(0d, aaTopLeft.PixelY, 6);
        Assert.Equal(100d, aaTopLeft.MachineX, 6);
        Assert.Equal(200d, aaTopLeft.MachineY, 6);
        Assert.Equal(105d, aaTopLeft.SafeMachineX, 6);
        Assert.Equal(205d, aaTopLeft.SafeMachineY, 6);

        var aaBottomRight = Assert.Single(snapshot.Points, point => point.Key == "aa-br");
        Assert.Equal(1000d, aaBottomRight.PixelX, 6);
        Assert.Equal(500d, aaBottomRight.PixelY, 6);
        Assert.Equal(300d, aaBottomRight.MachineX, 6);
        Assert.Equal(300d, aaBottomRight.MachineY, 6);
        Assert.Equal(295d, aaBottomRight.SafeMachineX, 6);
        Assert.Equal(295d, aaBottomRight.SafeMachineY, 6);

        var horizontalGuide = Assert.Single(snapshot.Lines, line => line.Key == "h-1");
        Assert.Equal(0d, horizontalGuide.StartPixelY, 6);
        Assert.Equal(200d, horizontalGuide.StartMachineY, 6);
        Assert.Equal(0d, horizontalGuide.StartPixelX, 6);
        Assert.Equal(1000d, horizontalGuide.EndPixelX, 6);

        var horizontalGuideLast = Assert.Single(snapshot.Lines, line => line.Key == "h-5");
        Assert.Equal(500d, horizontalGuideLast.StartPixelY, 6);
        Assert.Equal(300d, horizontalGuideLast.StartMachineY, 6);

        var verticalGuide = Assert.Single(snapshot.Lines, line => line.Key == "v-5");
        Assert.Equal(1000d, verticalGuide.StartPixelX, 6);
        Assert.Equal(300d, verticalGuide.StartMachineX, 6);
        Assert.Equal(0d, verticalGuide.StartPixelY, 6);
        Assert.Equal(500d, verticalGuide.EndPixelY, 6);

        var bistRect = snapshot.Rectangles[0];
        Assert.Equal(250d, bistRect.PixelLeft, 6);
        Assert.Equal(125d, bistRect.PixelTop, 6);
        Assert.Equal(750d, bistRect.PixelRight, 6);
        Assert.Equal(375d, bistRect.PixelBottom, 6);
        Assert.Equal(150d, bistRect.MachineLeft, 6);
        Assert.Equal(225d, bistRect.MachineTop, 6);
        Assert.Equal(250d, bistRect.MachineRight, 6);
        Assert.Equal(275d, bistRect.MachineBottom, 6);
        Assert.Equal(155d, bistRect.SafeMachineLeft, 6);
        Assert.Equal(230d, bistRect.SafeMachineTop, 6);
        Assert.Equal(245d, bistRect.SafeMachineRight, 6);
        Assert.Equal(270d, bistRect.SafeMachineBottom, 6);
    }

    [Fact]
    public void BuildSnapshot_ComputesCustomFourPointArrayWithSafeDots()
    {
        var grid = BuildGrid(rows: 2, cols: 2, cellWidth: 10d, cellHeight: 5d);
        var request = new CoordinatePlannerRequest(
            MachineOriginX: 100d,
            MachineOriginY: 200d,
            MachineWidth: 200d,
            MachineHeight: 100d,
            PixelWidth: 1000,
            PixelHeight: 500,
            HorizontalGuideCount: 0,
            VerticalGuideCount: 0,
            CopperPillarDiameter: 10d,
            ShowBistRectangle: false,
            ShowCustomArray: true,
            CustomArrayColumnCount: 3,
            CustomArrayRowCount: 2,
            CustomArrayTopLeftMachineX: 100d,
            CustomArrayTopLeftMachineY: 200d,
            CustomArrayTopRightMachineX: 300d,
            CustomArrayTopRightMachineY: 200d,
            CustomArrayBottomRightMachineX: 300d,
            CustomArrayBottomRightMachineY: 300d,
            CustomArrayBottomLeftMachineX: 100d,
            CustomArrayBottomLeftMachineY: 300d);

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(grid, request);

        Assert.Equal(14, snapshot.Points.Count);
        Assert.Equal(4, snapshot.Lines.Count(line => line.Kind == CoordinatePlannerLineKind.CustomArrayEdge));
        Assert.Equal(4, snapshot.Points.Count(point => point.Kind == CoordinatePlannerPointKind.CustomArrayCorner));
        Assert.Equal(6, snapshot.Points.Count(point => point.Kind == CoordinatePlannerPointKind.CustomArrayDot));

        var topLeftCorner = Assert.Single(snapshot.Points, point => point.Key == "array-tl");
        Assert.Equal(100d, topLeftCorner.MachineX, 6);
        Assert.Equal(200d, topLeftCorner.MachineY, 6);
        Assert.Equal(105d, topLeftCorner.SafeMachineX, 6);
        Assert.Equal(205d, topLeftCorner.SafeMachineY, 6);

        var firstDot = Assert.Single(snapshot.Points, point => point.Key == "array-dot-r1-c1");
        Assert.Equal(100d, firstDot.MachineX, 6);
        Assert.Equal(200d, firstDot.MachineY, 6);
        Assert.Equal(105d, firstDot.SafeMachineX, 6);
        Assert.Equal(205d, firstDot.SafeMachineY, 6);

        var middleDot = Assert.Single(snapshot.Points, point => point.Key == "array-dot-r1-c2");
        Assert.Equal(200d, middleDot.MachineX, 6);
        Assert.Equal(200d, middleDot.MachineY, 6);
        Assert.Equal(200d, middleDot.SafeMachineX, 6);
        Assert.Equal(205d, middleDot.SafeMachineY, 6);

        var lastDot = Assert.Single(snapshot.Points, point => point.Key == "array-dot-r2-c3");
        Assert.Equal(300d, lastDot.MachineX, 6);
        Assert.Equal(300d, lastDot.MachineY, 6);
        Assert.Equal(295d, lastDot.SafeMachineX, 6);
        Assert.Equal(295d, lastDot.SafeMachineY, 6);
    }

    [Fact]
    public void BuildSnapshot_WhenGuideBoundsDiffer_UsesGuideBoundsOnlyForGuideLines()
    {
        var activeAreaBounds = new Rect2(10d, 20d, 110d, 220d);
        var guideBounds = new Rect2(0d, 0d, 1d, 1d);
        var request = new CoordinatePlannerRequest(
            MachineOriginX: 0d,
            MachineOriginY: 0d,
            MachineWidth: 100d,
            MachineHeight: 200d,
            PixelWidth: 1000,
            PixelHeight: 2000,
            HorizontalGuideCount: 3,
            VerticalGuideCount: 3,
            CopperPillarDiameter: 0d,
            ShowBistRectangle: false,
            ShowCustomArray: false,
            CustomArrayColumnCount: 0,
            CustomArrayRowCount: 0,
            CustomArrayTopLeftMachineX: 0d,
            CustomArrayTopLeftMachineY: 0d,
            CustomArrayTopRightMachineX: 100d,
            CustomArrayTopRightMachineY: 0d,
            CustomArrayBottomRightMachineX: 100d,
            CustomArrayBottomRightMachineY: 200d,
            CustomArrayBottomLeftMachineX: 0d,
            CustomArrayBottomLeftMachineY: 200d);

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(activeAreaBounds, guideBounds, request);

        var aaTopLeft = Assert.Single(snapshot.Points, point => point.Key == "aa-tl");
        Assert.Equal(10d, aaTopLeft.World.X, 6);
        Assert.Equal(220d, aaTopLeft.World.Y, 6);

        var firstHorizontalGuide = Assert.Single(snapshot.Lines, line => line.Key == "h-1");
        Assert.Equal(0d, firstHorizontalGuide.StartWorld.X, 6);
        Assert.Equal(1d, firstHorizontalGuide.StartWorld.Y, 6);
        Assert.Equal(1d, firstHorizontalGuide.EndWorld.X, 6);
        Assert.Equal(1d, firstHorizontalGuide.EndWorld.Y, 6);

        var lastVerticalGuide = Assert.Single(snapshot.Lines, line => line.Key == "v-3");
        Assert.Equal(1d, lastVerticalGuide.StartWorld.X, 6);
        Assert.Equal(1d, lastVerticalGuide.StartWorld.Y, 6);
        Assert.Equal(1d, lastVerticalGuide.EndWorld.X, 6);
        Assert.Equal(0d, lastVerticalGuide.EndWorld.Y, 6);
    }

    [Fact]
    public void BuildSnapshot_WhenGuideModeIsPitch_UsesPitchAndInsetOffsets()
    {
        var activeAreaBounds = new Rect2(0d, 0d, 100d, 50d);
        var request = BuildGuideModeRequest(
            horizontalMode: CoordinateGuideGenerationMode.Pitch,
            verticalMode: CoordinateGuideGenerationMode.Pitch,
            horizontalPitch: 20d,
            verticalPitch: 30d,
            horizontalInset: 10d,
            verticalInset: 5d);

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(activeAreaBounds, request);

        var horizontalMachineY = snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide)
            .Select(static line => line.StartMachineY)
            .ToArray();
        var verticalMachineX = snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.VerticalGuide)
            .Select(static line => line.StartMachineX)
            .ToArray();

        Assert.Equal([10d, 30d, 50d, 70d, 90d], horizontalMachineY);
        Assert.Equal([5d, 35d, 65d, 95d], verticalMachineX);
    }

    [Fact]
    public void BuildSnapshot_WhenGuideModeIsExplicitPositions_FiltersByInsetAndSortsPositions()
    {
        var activeAreaBounds = new Rect2(0d, 0d, 100d, 50d);
        var request = BuildGuideModeRequest(
            horizontalMode: CoordinateGuideGenerationMode.ExplicitPositions,
            verticalMode: CoordinateGuideGenerationMode.ExplicitPositions,
            horizontalInset: 10d,
            verticalInset: 5d,
            horizontalPositions: [90d, 10d, 101d, 50d],
            verticalPositions: [2d, 95d, 40d]);

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(activeAreaBounds, request);

        var horizontalMachineY = snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.HorizontalGuide)
            .Select(static line => line.StartMachineY)
            .ToArray();
        var verticalMachineX = snapshot.Lines
            .Where(static line => line.Kind == CoordinatePlannerLineKind.VerticalGuide)
            .Select(static line => line.StartMachineX)
            .ToArray();

        Assert.Equal([10d, 50d, 90d], horizontalMachineY);
        Assert.Equal([40d, 95d], verticalMachineX);
    }

    [Fact]
    public void BuildSnapshot_WhenCustomPointAndPathProvided_EmitsCustomArtifacts()
    {
        var activeAreaBounds = new Rect2(0d, 0d, 100d, 100d);
        var request = BuildGuideModeRequest(
            horizontalMode: CoordinateGuideGenerationMode.Count,
            verticalMode: CoordinateGuideGenerationMode.Count,
            horizontalPositions: null,
            verticalPositions: null) with
        {
            CustomPoints =
            [
                new CoordinateCustomPointRequest("1", "Probe A", 25d, 30d),
            ],
            CustomPaths =
            [
                new CoordinateCustomPathRequest("1", "Edge sweep", 10d, 20d, 90d, 20d, 3),
            ],
        };

        var snapshot = CoordinatePlannerComputationService.BuildSnapshot(activeAreaBounds, request);

        var customPoint = Assert.Single(snapshot.Points, point => point.Kind == CoordinatePlannerPointKind.CustomPoint);
        Assert.Equal("custom-point-1", customPoint.Key);
        Assert.Equal(25d, customPoint.MachineX, 6);
        var customPath = Assert.Single(snapshot.Lines, line => line.Kind == CoordinatePlannerLineKind.CustomPath);
        Assert.Equal("custom-path-1", customPath.Key);
        Assert.Equal(10d, customPath.StartMachineX, 6);
        Assert.Equal(90d, customPath.EndMachineX, 6);
        var pathSteps = snapshot.Points
            .Where(static point => point.Kind == CoordinatePlannerPointKind.CustomPathStep)
            .ToArray();
        Assert.Equal(3, pathSteps.Length);
        Assert.Equal("custom-path-1-step-2", pathSteps[1].Key);
        Assert.Equal(50d, pathSteps[1].MachineX, 6);
    }

    private static void AssertSnapshot(string expected, CoordinatePlannerSnapshot snapshot)
    {
        var rows = new List<string>
        {
            FormattableString.Invariant($"AA={snapshot.ActiveAreaBounds.MinX:R},{snapshot.ActiveAreaBounds.MinY:R},{snapshot.ActiveAreaBounds.MaxX:R},{snapshot.ActiveAreaBounds.MaxY:R}|radius={snapshot.CopperPillarRadiusMachine:R}|worldRadius={snapshot.WorldPillarRadiusX:R},{snapshot.WorldPillarRadiusY:R}"),
        };
        rows.AddRange(snapshot.Points.Select(static point => FormattableString.Invariant(
            $"P|{point.Key}|{point.Label}|{point.Kind}|world={point.World.X:R},{point.World.Y:R}|safeWorld={point.SafeWorld.X:R},{point.SafeWorld.Y:R}|pixel={point.PixelX:R},{point.PixelY:R}|machine={point.MachineX:R},{point.MachineY:R}|safeMachine={point.SafeMachineX:R},{point.SafeMachineY:R}")));
        rows.AddRange(snapshot.Lines.Select(static line => FormattableString.Invariant(
            $"L|{line.Key}|{line.Label}|{line.Kind}|world={line.StartWorld.X:R},{line.StartWorld.Y:R},{line.EndWorld.X:R},{line.EndWorld.Y:R}|safeWorld={line.SafeStartWorld.X:R},{line.SafeStartWorld.Y:R},{line.SafeEndWorld.X:R},{line.SafeEndWorld.Y:R}|pixel={line.StartPixelX:R},{line.StartPixelY:R},{line.EndPixelX:R},{line.EndPixelY:R}|machine={line.StartMachineX:R},{line.StartMachineY:R},{line.EndMachineX:R},{line.EndMachineY:R}|safeMachine={line.SafeStartMachineX:R},{line.SafeStartMachineY:R},{line.SafeEndMachineX:R},{line.SafeEndMachineY:R}")));
        rows.AddRange(snapshot.Rectangles.Select(static rectangle => FormattableString.Invariant(
            $"R|{rectangle.Key}|{rectangle.Label}|{rectangle.Kind}|world={rectangle.WorldBounds.MinX:R},{rectangle.WorldBounds.MinY:R},{rectangle.WorldBounds.MaxX:R},{rectangle.WorldBounds.MaxY:R}|safeWorld={rectangle.SafeWorldBounds.MinX:R},{rectangle.SafeWorldBounds.MinY:R},{rectangle.SafeWorldBounds.MaxX:R},{rectangle.SafeWorldBounds.MaxY:R}|pixel={rectangle.PixelLeft:R},{rectangle.PixelTop:R},{rectangle.PixelRight:R},{rectangle.PixelBottom:R}|machine={rectangle.MachineLeft:R},{rectangle.MachineTop:R},{rectangle.MachineRight:R},{rectangle.MachineBottom:R}|safeMachine={rectangle.SafeMachineLeft:R},{rectangle.SafeMachineTop:R},{rectangle.SafeMachineRight:R},{rectangle.SafeMachineBottom:R}")));

        var actual = string.Join('\n', rows);
        Assert.Equal(expected.Replace("\r\n", "\n", StringComparison.Ordinal), actual);
    }

    private static RegularGrid BuildGrid(int rows, int cols, double cellWidth, double cellHeight)
    {
        var pads = new List<RegularPad>(rows * cols);
        var xEdges = Enumerable.Range(0, cols + 1).Select(value => value * cellWidth).ToArray();
        var yEdges = Enumerable.Range(0, rows + 1).Select(value => value * cellHeight).ToArray();

        for (var row = 0; row < rows; row++)
        {
            for (var col = 0; col < cols; col++)
            {
                var left = col * cellWidth;
                var top = row * cellHeight;
                pads.Add(new RegularPad(
                    row,
                    col,
                    row * cols + col,
                    new Polygon2(new[]
                    {
                        new Point2(left, top),
                        new Point2(left + cellWidth, top),
                        new Point2(left + cellWidth, top + cellHeight),
                        new Point2(left, top + cellHeight),
                    })));
            }
        }

        return new RegularGrid(rows, cols, xEdges, yEdges, pads);
    }

    private static CoordinatePlannerRequest BuildGuideModeRequest(
        CoordinateGuideGenerationMode horizontalMode,
        CoordinateGuideGenerationMode verticalMode,
        double horizontalPitch = 0d,
        double verticalPitch = 0d,
        double horizontalInset = 0d,
        double verticalInset = 0d,
        IReadOnlyList<double>? horizontalPositions = null,
        IReadOnlyList<double>? verticalPositions = null)
    {
        return new CoordinatePlannerRequest(
            MachineOriginX: 0d,
            MachineOriginY: 0d,
            MachineWidth: 100d,
            MachineHeight: 100d,
            PixelWidth: 1000,
            PixelHeight: 1000,
            HorizontalGuideCount: 0,
            VerticalGuideCount: 0,
            CopperPillarDiameter: 0d,
            ShowBistRectangle: false,
            ShowCustomArray: false,
            CustomArrayColumnCount: 0,
            CustomArrayRowCount: 0,
            CustomArrayTopLeftMachineX: 0d,
            CustomArrayTopLeftMachineY: 0d,
            CustomArrayTopRightMachineX: 100d,
            CustomArrayTopRightMachineY: 0d,
            CustomArrayBottomRightMachineX: 100d,
            CustomArrayBottomRightMachineY: 100d,
            CustomArrayBottomLeftMachineX: 0d,
            CustomArrayBottomLeftMachineY: 100d,
            HorizontalGuideMode: horizontalMode,
            VerticalGuideMode: verticalMode,
            HorizontalGuidePitch: horizontalPitch,
            VerticalGuidePitch: verticalPitch,
            HorizontalGuideInset: horizontalInset,
            VerticalGuideInset: verticalInset,
            HorizontalGuidePositions: horizontalPositions,
            VerticalGuidePositions: verticalPositions);
    }
}
