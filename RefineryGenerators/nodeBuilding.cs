﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Autodesk.DesignScript.Runtime;
using Autodesk.DesignScript.Geometry;

namespace Buildings
{
    public static class Creation
    {
        /// <summary>
        /// Typology selection
        /// </summary>
        /// <param name="selection">Select building type by index (ex. U, L, I, H, O, D)</param>
        /// <returns></returns>
        /// <search>building,design,refinery</search>
        [MultiReturn(new[] { "SelectedType" })]
        public static Dictionary<string, object> SelectBuildingType(int selection)
        {
            string selected = "H";

            // return a dictionary
            return new Dictionary<string, object>
            {
                {"SelectedType", selected},
            };
        }

        /// <summary>
        /// Generates a building mass
        /// </summary>
        /// <param name="Type">Building type (ex. U, L, I, H, O, D)</param>
        /// <param name="BasePlane">The building base plane.</param>
        /// <param name="Length">Overall building length.</param>
        /// <param name="Width">Overall building width.</param>
        /// <param name="Depth">Building depth.</param>
        /// <param name="BldgArea">Target gross building area.</param>
        /// <param name="FloorHeight">Height of the floor.</param>
        /// <param name="CreateCore">Create core volumes and subtractions?</param>
        /// <returns></returns>
        /// <search>building,design,refinery</search>
        [MultiReturn(new[] { "Floors", "Mass", "Cores", "TotalFloorArea", "BuildingVolume", "TopPlane" })]
        public static Dictionary<string, object> BuildingGenerator(string Type, Plane BasePlane, double Length, double Width, double Depth, double BldgArea, double FloorHeight, bool CreateCore)
        {
            var floors = new List<Surface>();
            PolySurface mass = null;
            List<PolySurface> cores = null;
            double totalArea = 0;
            double totalVolume = 0;
            Plane topPlane = null;

            if (Length <= 0 || Width <= 0 || Depth <= 0 || BldgArea <= 0 || FloorHeight <= 0)
            {
                return new Dictionary<string, object>();
            }

            var baseSurface = MakeBaseSurface(Type, Length, Width, Depth);

            if (baseSurface != null)
            {
                // Surface is constructed with lower left corner at (0,0). Move and rotate to given base plane.
                baseSurface = (Surface)baseSurface.Transform(CoordinateSystem.ByOrigin(Width / 2, Length / 2), BasePlane.ToCoordinateSystem());

                double floorCount = Math.Ceiling(BldgArea / baseSurface.Area);
                Solid solid = baseSurface.Thicken(floorCount * FloorHeight, both_sides: false);

                mass = PolySurface.BySolid(solid);

                for (int i = 0; i < floorCount; i++)
                {
                    floors.Add((Surface)baseSurface.Translate(Vector.ByCoordinates(0, 0, i * FloorHeight)));
                }

                totalArea = baseSurface.Area * floorCount;

                totalVolume = solid.Volume;

                topPlane = (Plane)BasePlane.Translate(Vector.ByCoordinates(0, 0, floorCount * FloorHeight));
            }

            // return a dictionary
            return new Dictionary<string, object>
            {
                {"Floors", floors},
                {"Mass", mass},
                {"Cores", cores},
                {"TotalFloorArea", totalArea},
                {"BuildingVolume", totalVolume},
                {"TopPlane", topPlane}
            };
        }

        private static Surface MakeBaseSurface(string Type, double Length, double Width, double Depth)
        {
            Curve boundary = null;
            var holes = new List<Curve>();
            Surface baseSurface = null;

            switch (Type)
            {
                case "I":
                    boundary = PolyCurve.ByPoints(new[]
                    {
                                Point.ByCoordinates(0, 0),
                                Point.ByCoordinates(Width, 0),
                                Point.ByCoordinates(Width, Length),
                                Point.ByCoordinates(0, Length)
                            }, connectLastToFirst: true);
                    break;

                case "U":
                    if (Length <= Depth || Width <= Depth * 2)
                    {
                        break;
                    }

                    if (Length > Width / 2)
                    {
                        // Enough room to make the curved part of the U an arc.

                        // Center-point of the curved parts of the U.
                        var uArcCenter = Point.ByCoordinates(Width / 2, Width / 2);

                        boundary = PolyCurve.ByJoinedCurves(new Curve[]
                        {
                                    PolyCurve.ByPoints(new[]
                                    {
                                        Point.ByCoordinates(0, Width / 2),
                                        Point.ByCoordinates(0, Length),
                                        Point.ByCoordinates(Depth, Length),
                                        Point.ByCoordinates(Depth, Width / 2)
                                    }),
                                    Arc.ByCenterPointStartPointEndPoint(
                                        uArcCenter,
                                        Point.ByCoordinates(Depth, Width / 2),
                                        Point.ByCoordinates(Width - Depth, Width / 2)
                                    ),
                                    PolyCurve.ByPoints(new[]
                                    {
                                        Point.ByCoordinates(Width - Depth, Width / 2),
                                        Point.ByCoordinates(Width - Depth, Length),
                                        Point.ByCoordinates(Width, Length),
                                        Point.ByCoordinates(Width, Width / 2)
                                    }),
                                    Arc.ByCenterPointStartPointEndPoint(
                                        uArcCenter,
                                        Point.ByCoordinates(0, Width / 2),
                                        Point.ByCoordinates(Width, Width / 2)
                                    )
                        });
                    }
                    else
                    {
                        // Short U. Use ellipses and no straight part.
                        var ellipseCenter = Plane.ByOriginNormal(
                            Point.ByCoordinates(Width / 2, Length),
                            Vector.ZAxis());

                        boundary = PolyCurve.ByJoinedCurves(new Curve[]
                        {
                                    Line.ByStartPointEndPoint(
                                        Point.ByCoordinates(Width, Length),
                                        Point.ByCoordinates(Width - Depth, Length)),
                                    EllipseArc.ByPlaneRadiiAngles(ellipseCenter, Width / 2 - Depth, Length - Depth, 180, 180),
                                    Line.ByStartPointEndPoint(
                                        Point.ByCoordinates(Depth, Length),
                                        Point.ByCoordinates(0, Length)),
                                    EllipseArc.ByPlaneRadiiAngles(ellipseCenter, Width / 2, Length, 180, 180)
                        });
                    }

                    break;

                case "L":
                    if (Width <= Depth || Length <= Depth)
                    {
                        break;
                    }

                    boundary = PolyCurve.ByPoints(new[]
                    {
                                Point.ByCoordinates(0, 0),
                                Point.ByCoordinates(Width, 0),
                                Point.ByCoordinates(Width, Depth),
                                Point.ByCoordinates(Depth, Depth),
                                Point.ByCoordinates(Depth, Length),
                                Point.ByCoordinates(0, Length)
                            }, connectLastToFirst: true);
                    break;

                case "H":
                    if (Width <= Depth * 2 || Length <= Depth)
                    {
                        break;
                    }

                    boundary = PolyCurve.ByPoints(new[]
                    {
                                Point.ByCoordinates(0, 0),
                                Point.ByCoordinates(Depth, 0),
                                Point.ByCoordinates(Depth, (Length - Depth) / 2),
                                Point.ByCoordinates(Width - Depth, (Length - Depth) / 2),
                                Point.ByCoordinates(Width - Depth, 0),
                                Point.ByCoordinates(Width, 0),
                                Point.ByCoordinates(Width, Length),
                                Point.ByCoordinates(Width - Depth, Length),
                                Point.ByCoordinates(Width - Depth, (Length + Depth) / 2),
                                Point.ByCoordinates(Depth, (Length + Depth) / 2),
                                Point.ByCoordinates(Depth, Length),
                                Point.ByCoordinates(0, Length)
                            }, connectLastToFirst: true);
                    break;

                case "D":
                    if (Width <= Depth * 2 || Length <= Depth * 2)
                    {
                        break;
                    }

                    // The D is pointing "down" so that it matches with the U.

                    if (Length > Width / 2 + Depth)
                    {
                        // Enough room to make the curved part of the D an arc.

                        // Center-point of the curved parts of the D.
                        var dArcCenter = Point.ByCoordinates(Width / 2, Width / 2);

                        boundary = PolyCurve.ByJoinedCurves(new Curve[]
                        {
                                    PolyCurve.ByPoints(new[]
                                    {
                                        Point.ByCoordinates(Width, Width / 2),
                                        Point.ByCoordinates(Width, Length),
                                        Point.ByCoordinates(0, Length),
                                        Point.ByCoordinates(0, Width / 2)
                                    }),
                                    Arc.ByCenterPointStartPointEndPoint(
                                        dArcCenter,
                                        Point.ByCoordinates(0, Width / 2),
                                        Point.ByCoordinates(Width, Width / 2)
                                    )
                        });

                        holes.Add(PolyCurve.ByJoinedCurves(new Curve[]
                        {
                                    PolyCurve.ByPoints(new[]
                                    {
                                        Point.ByCoordinates(Width - Depth, Width / 2),
                                        Point.ByCoordinates(Width - Depth, Length - Depth),
                                        Point.ByCoordinates(Depth, Length - Depth),
                                        Point.ByCoordinates(Depth, Width / 2)
                                    }),
                                    Arc.ByCenterPointStartPointEndPoint(
                                        dArcCenter,
                                        Point.ByCoordinates(Depth, Width / 2),
                                        Point.ByCoordinates(Width - Depth, Width / 2)
                                    )
                        }));
                    }
                    else
                    {
                        // Short D. Use ellipses and no straight part.
                        var ellipseCenter = Plane.ByOriginNormal(
                            Point.ByCoordinates(Width / 2, Length - Depth),
                            Vector.ZAxis());

                        boundary = PolyCurve.ByJoinedCurves(new Curve[]
                        {
                                    PolyCurve.ByPoints(new[]
                                    {
                                        Point.ByCoordinates(Width, Length - Depth),
                                        Point.ByCoordinates(Width, Length),
                                        Point.ByCoordinates(0, Length),
                                        Point.ByCoordinates(0, Length - Depth)
                                    }),
                                    EllipseArc.ByPlaneRadiiAngles(ellipseCenter, Width / 2, Length - Depth, 180, 180)
                        });

                        holes.Add(PolyCurve.ByJoinedCurves(new Curve[]
                        {
                                    Line.ByStartPointEndPoint(
                                        Point.ByCoordinates(Width - Depth, Length - Depth),
                                        Point.ByCoordinates(Depth, Length - Depth)),
                                    EllipseArc.ByPlaneRadiiAngles(ellipseCenter, Width / 2 - Depth, Length - 2 * Depth, 180, 180)
                        }));
                    }

                    break;

                case "O":
                    if (Width <= Depth * 2 || Length <= Depth * 2)
                    {
                        break;
                    }

                    var centerPoint = Point.ByCoordinates(Width / 2, Length / 2);

                    boundary = Ellipse.ByOriginRadii(centerPoint, Width / 2, Length / 2);
                    holes.Add(Ellipse.ByOriginRadii(centerPoint, (Width / 2) - Depth, (Length / 2) - Depth));

                    break;
            }

            if (boundary != null)
            {
                baseSurface = Surface.ByPatch(boundary);

                if (holes.Count > 0)
                {
                    // A bug in Dynamo requires the boundary curve to be included in the trim curves, otherwise it trims the wrong part.
                    holes.Add(boundary);
                    baseSurface = baseSurface.TrimWithEdgeLoops(holes.Select(c => PolyCurve.ByJoinedCurves(new[] { c })));
                }
            }

            return baseSurface;
        }
    }

    public static class Analysis
    {
        /// <summary>
        /// Deconstructs a building mass into component horizontal and vertical parts 
        /// </summary>
        /// <param name="Mass">Building mass</param>
        /// <param name="tolerance">Tolerance for vertical and horizontal classification</param>
        /// <returns></returns>
        /// <search>building,design,refinery</search>
        [MultiReturn(new[] { "VerticalSurfaces", "HoriztonalSurfaces" })]
        public static Dictionary<string, object> DeceonstructFacadeShell(PolySurface Mass, double tolerance)
        {
            List<Surface> horizontal = null;
            List<Surface> vertical = null;

            // return a dictionary
            return new Dictionary<string, object>
            {
                {"VerticalSurfaces", horizontal},
                {"HorizontalSurfaces", vertical}
            };
        }
    }
}
