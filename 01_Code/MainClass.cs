using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Globalization;

using Autodesk.Revit.DB;
//using Autodesk.Revit.DB.Architecture;
//using Autodesk.Revit.UI;
//using Autodesk.Revit.UI.Selection;
//using Autodesk.Revit.ApplicationServices;
//using Autodesk.Revit.Attributes;
//using RevitServices;
//using RevitServices.Persistence;
using Autodesk.DesignScript.Runtime;
using ds_geom = Autodesk.DesignScript.Geometry;
using System.Reflection;
using System.IO;

namespace CreateTopoTest
{
    public class MainClass
    {
        private MainClass() //List<XYZ> Group1, List<PolymeshFacet> Group2
        {
            //this.Topo_P = Group1;
            //this.Topo_F = Group2;
        }
        /// <summary>
        /// Info abiut package
        /// </summary>
        /// <returns></returns>
        public static string AboutPackage()
        {
            return "Look package's github repo with sample dyn-scripts via link - https://github.com/GeorgGrebenyuk/TopoSurfaceToRevit";
        }
        /// <summary>
        /// This node create a Revit TopographySurface enity by List with 3-dimentiolal Points (XYZ) and List by PolymeshFacet
        /// </summary>
        /// <param name="Topo_Points">List with 3D-Points (XYZ type)</param>
        /// <param name="Topo_Faces">List with Faces of surface (PolymeshFacet type)</param>
        public static void CreateTopo(List<XYZ> Topo_Points, List<PolymeshFacet> Topo_Faces)
        {
            var RevitDoc = RevitServices.Persistence.DocumentManager.Instance.CurrentDBDocument;

            //GetPointsAndFacesFromLandxml(PathToLandxml);

            Transaction trans = new Transaction(RevitDoc);
            trans.Start("Create topo surface");
            Autodesk.Revit.DB.Architecture.TopographySurface.Create(RevitDoc, Topo_Points, Topo_Faces);
            trans.Commit();
        }
        private static double BP_X = 0d;
        private static double BP_Y = 0d;
        private static double BP_Z = 0d;
        /// <summary>
        /// Method to fing and return coordinates of project's internal survey's point
        /// </summary>
        public static double[] GetBasePointCoords()
        {
            double[] BP = new double[3];
            var RevitDoc = RevitServices.Persistence.DocumentManager.Instance.CurrentDBDocument;
            Transaction trans = new Transaction(RevitDoc);
            trans.Start("Get Base (survey) point coordinates");
            var basePoint = new FilteredElementCollector(RevitDoc).OfClass(typeof(BasePoint)).Cast<BasePoint>().Where(p => p.IsShared).ToList().FirstOrDefault();

            BP_Y = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_NORTHSOUTH_PARAM).AsDouble();
            BP_X = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_EASTWEST_PARAM).AsDouble();
            BP_Z = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_ELEVATION_PARAM).AsDouble();
            BP[0] = BP_X; BP[1] = BP_Y; BP[2] = BP_Z;
            //BasePoint_Angle = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_ANGLETON_PARAM).AsDouble();

            trans.Commit();
            return BP;
        }
        /// <summary>
        /// Node that convert landxml's data to Lists with Points and faces
        /// </summary>
        /// <param name="PathToLandxml">Absolute file's path to landxml file</param>
        /// <param name="UnitsCoeff">Auxilary double-value coefficient (between meters in landxml surface definition to Revit's units). Optionally = 1000 </param>
        /// <returns></returns>
        [MultiReturn(new[] { "Topo_Points", "Topo_Faces" })]
        public static Dictionary<string, object> GetPointsAndFacesFromLandxml(string PathToLandxml, ds_geom.Point RevitsBasePoint, double UnitsCoeff = 1d / 0.3048) //Dictionary <string,object>
        {
            //GetBasePointCoords();
            BP_X = RevitsBasePoint.X;
            BP_Y = RevitsBasePoint.Y;
            BP_Z = RevitsBasePoint.Z;

            XDocument SourceLandXml = XDocument.Load(PathToLandxml);
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            List<XYZ> Topo_Points = new List<XYZ>();
            List<XYZ> Topo_Points_temp = new List<XYZ>();
            List<PolymeshFacet> Topo_Faces = new List<PolymeshFacet>();

            XElement el_Pnts = SourceLandXml.Descendants().Where(a => a.Name.LocalName == "Pnts").First();
            IEnumerable<XElement> el_PntsCollection = el_Pnts.Elements().Where(a => a.Name.LocalName == "P");

            XElement el_Faces = SourceLandXml.Descendants().Where(a => a.Name.LocalName == "Faces").First();
            IEnumerable<XElement> el_FacesCollection = el_Faces.Elements().Where(a => a.Name.LocalName == "F");

            string[] IndexesOdPoints = new string[el_PntsCollection.Count()];
            void AddPntsToList(List<XYZ> ToSave)
            {
                int i1 = 0;
                foreach (var OnePoint in el_PntsCollection)
                {
                    double[] PntsCoords = OnePoint.Value.Split(' ').Select(x => double.Parse(x, CultureInfo.GetCultureInfo("en-US")) * UnitsCoeff).ToArray();
                    XYZ new_Point = new XYZ(PntsCoords[1] - BP_X, PntsCoords[0] - BP_Y, PntsCoords[2] - BP_Z);
                    ToSave.Add(new_Point);
                    IndexesOdPoints[i1] = OnePoint.Attribute("id").Value;
                    i1++;
                }
            }
            AddPntsToList(Topo_Points);
            foreach (var OneFace in el_FacesCollection)
            {
                int[] PntsNums = OneFace.Value.Split(' ').Select(x => int.Parse(x, CultureInfo.GetCultureInfo("en-US"))).ToArray();
                PolymeshFacet new_Face = new PolymeshFacet(GetIndexOfFacetsPoint(PntsNums[0]), GetIndexOfFacetsPoint(PntsNums[1]), GetIndexOfFacetsPoint(PntsNums[2]));
                Topo_Faces.Add(new_Face);
            }

            //Auxilay method that return true Point's position by point's id in Face's definition
            int GetIndexOfFacetsPoint(int CurrentIndexInFace)
            {
                for (int i2 = 0; i2 < IndexesOdPoints.Length; i2++)
                {
                    if (IndexesOdPoints[i2] == CurrentIndexInFace.ToString())
                    {
                        return i2;
                    }
                }
                return 0;

            }

            return new Dictionary<string, object>()
            {
                {"Topo_Points",Topo_Points},
                {"Topo_Faces", Topo_Faces}
            };
        }

        /// <summary>
        /// Node that convert landxml's data to Lists with Points and faces
        /// </summary>
        /// <param name="PathToLandxml">Absolute file's path to landxml file</param>
        /// <param name="UnitsCoeff">Auxilary double-value coefficient (between meters in landxml surface definition to Revit's units). Optionally = 1000 </param>
        /// <param name="Min_point">Minimum point - left bottom in internal Revit's coordinates</param>
        /// <param name="Max_point">Maximum point - upper right in internal Revit's coordinates</param>
        /// <returns></returns>
        [MultiReturn(new[] { "Topo_Points", "Topo_Faces", "Count of cutting points" })]
        private static Dictionary<string, object> GetPointsAndFacesFromLandxml_WithCuting(string PathToLandxml, ds_geom.Point Min_point, ds_geom.Point Max_point, ds_geom.Point RevitsBasePoint, double UnitsCoeff = 1d / 0.3048) //Dictionary <string,object>
        {
            //GetBasePointCoords();
            BP_X = RevitsBasePoint.X;
            BP_Y = RevitsBasePoint.Y;
            BP_Z = RevitsBasePoint.Z;

            XDocument SourceLandXml = XDocument.Load(PathToLandxml);
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            List<XYZ> Topo_Points = new List<XYZ>();
            List<XYZ> Topo_Points_temp = new List<XYZ>();
            List<PolymeshFacet> Topo_Faces = new List<PolymeshFacet>();

            XElement el_Pnts = SourceLandXml.Descendants().Where(a => a.Name.LocalName == "Pnts").First();
            IEnumerable<XElement> el_PntsCollection = el_Pnts.Elements().Where(a => a.Name.LocalName == "P");

            XElement el_Faces = SourceLandXml.Descendants().Where(a => a.Name.LocalName == "Faces").First();
            IEnumerable<XElement> el_FacesCollection = el_Faces.Elements().Where(a => a.Name.LocalName == "F");

            string[] IndexesOdPoints = new string[el_PntsCollection.Count()];
            void AddPntsToList(List<XYZ> ToSave)
            {
                int i1 = 0;
                foreach (var OnePoint in el_PntsCollection)
                {
                    double[] PntsCoords = OnePoint.Value.Split(' ').Select(x => double.Parse(x, CultureInfo.GetCultureInfo("en-US")) * UnitsCoeff).ToArray();
                    XYZ new_Point = new XYZ(PntsCoords[1] - BP_X, PntsCoords[0] - BP_Y, PntsCoords[2] - BP_Z);
                    ToSave.Add(new_Point);
                    IndexesOdPoints[i1] = OnePoint.Attribute("id").Value;
                    i1++;
                }
            }
            Dictionary<int, int> NewByOldPoints = new Dictionary<int, int>();
            int CountOfFindedPoints = 0;

            AddPntsToList(Topo_Points_temp);
            List<int> new_pnts_ids = new List<int>();
            int i3 = 0;

            foreach (var OnePoint in Topo_Points_temp)
            {
                if (OnePoint.X >= Min_point.X && OnePoint.Y >= Min_point.Y && OnePoint.X <= Max_point.X && OnePoint.Y <= Max_point.Y)
                {
                    new_pnts_ids.Add(i3);
                }
                i3++;
            }
            CountOfFindedPoints = new_pnts_ids.Count();
            int i4 = 0;
            foreach (var OneFace in el_FacesCollection)
            {
                int[] PntsNums = OneFace.Value.Split(' ').Select(x => int.Parse(x, CultureInfo.GetCultureInfo("en-US"))).ToArray();
                bool NeedInclude = false;
                foreach (int OneIndexPoint in PntsNums)
                {
                    if (new_pnts_ids.Contains(OneIndexPoint))
                    {
                        NeedInclude = true; break;
                    }
                }
                if (NeedInclude == true)
                {
                    foreach (int OneIndexPoint in PntsNums)
                    {
                        //int OldNum = GetIndexOfFacetsPoint(OneIndexPoint);
                        if (!Topo_Points.Contains(Topo_Points_temp[OneIndexPoint]))
                        {
                            Topo_Points.Add(Topo_Points_temp[OneIndexPoint]);
                            NewByOldPoints.Add(OneIndexPoint, i4);
                            i4++;
                        }
                    }

                    Topo_Faces.Add(new PolymeshFacet(NewByOldPoints[PntsNums[0]], NewByOldPoints[PntsNums[1]], NewByOldPoints[PntsNums[2]]));

                }
            }


            //Auxilay method that return true Point's position by point's id in Face's definition
            int GetIndexOfFacetsPoint(int CurrentIndexInFace)
            {
                for (int i2 = 0; i2 < IndexesOdPoints.Length; i2++)
                {
                    if (IndexesOdPoints[i2] == CurrentIndexInFace.ToString())
                    {
                        return i2;
                    }
                }
                return 0;

            }

            return new Dictionary<string, object>()
            {
                {"Topo_Points",Topo_Points},
                {"Topo_Faces", Topo_Faces},
                {"Count of cutting points", CountOfFindedPoints},
            };
        }

        public static void CreateLandxmlByTopography (IEnumerable< ds_geom.IndexGroup> Faces, IEnumerable<ds_geom.Point> Points, ds_geom.Point BasePoint_coords, double coeff, string PathToSaveFile)
        {
            BP_X = BasePoint_coords.X; BP_Y = BasePoint_coords.Y; BP_Z = BasePoint_coords.Z;
            XDocument LandXml_Template;
            XNamespace xmlns_ns = "http://www.landxml.org/schema/LandXML-1.2";
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("CreateTopoTest.Res.Landxml_template.xml"))

            using (StreamReader reader = new StreamReader(stream))
            {
                string XmlString = reader.ReadToEnd();
                LandXml_Template = XDocument.Parse(XmlString);
            }
            //Создаем списки на базе граней и точек
            XElement el_Pnts = LandXml_Template.Descendants().Where(a => a.Name.LocalName == "Pnts").First();
            XElement el_Faces = LandXml_Template.Descendants().Where(a => a.Name.LocalName == "Faces").First();

            List<ds_geom.Point> SourcePoints = Points.ToList();
            List<ds_geom.IndexGroup> SourceFaces = Faces.ToList();

            for (int i1 = 0; i1< SourcePoints.Count(); i1++)
            {
                ds_geom.Point OnePoint = SourcePoints[i1];
                XElement new_Point = new XElement(xmlns_ns + "P", $"{OnePoint.Y/ coeff + BP_Y} {OnePoint.X/ coeff + BP_X} {OnePoint.Z/ coeff + BP_Z}", new XAttribute("id", $"{i1}"));
                el_Pnts.Add(new_Point);
            }
            for (int i1 = 0; i1 < SourceFaces.Count(); i1++)
            {
                ds_geom.IndexGroup OneFace = SourceFaces[i1];
                int ind1 = Convert.ToInt32(OneFace.A); int ind2 = Convert.ToInt32(OneFace.B); int ind3 = Convert.ToInt32(OneFace.C);

                XElement new_Face = new XElement(xmlns_ns + "F", $"{ind1} {ind2} {ind3}"); //new XAttribute("n", $"{ind1} {ind2} {ind3}")
                el_Faces.Add(new_Face);
            }
            LandXml_Template.Save(PathToSaveFile);

        }
        /// <summary>
        /// Create IFC file by source landxml surfaces's definition. Use an offset-point and value of rotation angle to place ifc-surface by selected basepoint
        /// </summary>
        /// <param name="PathToLandxmlFile">Absolute file-path to landxml file</param>
        /// <param name="ZeroPoint">Point that = zero in internal CS of model</param>
        /// <param name="RotatinoAngle">double value of rotation's angle in grades</param>

        public static void CreateIfcRepresentationOfSurfaceByLandxml (string PathToLandxmlFile, ds_geom.Point ZeroPoint, double RotatinoAngle)
        {
            RotatinoAngle = RotatinoAngle / 180 * Math.PI;

            XDocument source_file = XDocument.Load(PathToLandxmlFile);
            string IFC_FilePath = PathToLandxmlFile.Replace(".xml", ".ifc");
            XElement el_Surface = source_file.Descendants().Where(a => a.Name.LocalName == "Surface").First();
            XElement el_Pnts = el_Surface.Descendants().Where(a => a.Name.LocalName == "Pnts").First();
            XElement el_Faces = el_Surface.Descendants().Where(a => a.Name.LocalName == "Faces").First();

            //Записываем файл ресурсов - шаблон IFC во внешний файл
            var assembly = Assembly.GetExecutingAssembly();
            using (Stream stream = assembly.GetManifestResourceStream("CreateTopoTest.Res.Template.ifc"))

            using (StreamReader reader = new StreamReader(stream))
            {
                File.WriteAllText(IFC_FilePath, reader.ReadToEnd());
            }
            string InfoAboutLocation = null;
            string InfoAboutAngle = null;
            InfoAboutLocation = $"#43= IFCCARTESIANPOINT((0.,0.,0.));";
            InfoAboutAngle = $"#45= IFCDIRECTION((1.,0.,0.));";
            File.AppendAllText(IFC_FilePath, InfoAboutLocation + Environment.NewLine);
            File.AppendAllText(IFC_FilePath, InfoAboutAngle + Environment.NewLine);

            List<string> PointsNumbers = new List<string>();
            long NumMax = 0;

            double [] GetTransformedPoint (double [] CurrentPoint)
            {
                double x_new = (CurrentPoint[1] - ZeroPoint.X) * Math.Cos(RotatinoAngle) - (CurrentPoint[0] - ZeroPoint.Y) * Math.Sin(RotatinoAngle);
                double y_new = (CurrentPoint[1] - ZeroPoint.X) * Math.Sin(RotatinoAngle) + (CurrentPoint[0] - ZeroPoint.Y) * Math.Cos(RotatinoAngle);
                double z_new = CurrentPoint[2] - ZeroPoint.Z;
                return new double[3] { x_new, y_new, z_new };
                //return ds_geom.Point.ByCoordinates(x_new, y_new, z_new);
            }

            void AddInfoAboutPoint(string PointNum)
            {
                PointsNumbers.Add(PointNum);
                char K = '"'; //Символ кавычки в параметре id

                XElement OnePoint = el_Pnts.Elements().Where(a => a.Attribute("id").Value.Replace(K.ToString(), string.Empty) == PointNum).First();
                double [] PointCoordinates = OnePoint.Value.Split(' ').Select(x => double.Parse(x, CultureInfo.GetCultureInfo("en-US"))).ToArray();
                double[] RecalculatedPoint = GetTransformedPoint(PointCoordinates);
                string NewPointCoord = null;
                
                //Пока исполтзуется только для прямого пересчета координат
                NewPointCoord =
                    (RecalculatedPoint[0] * 1000).ToString() + "," +
                    (RecalculatedPoint[1] * 1000).ToString() + "," +
                    (RecalculatedPoint[2] * 1000).ToString();

                string InfoAboutPoint = $"#{Convert.ToInt64(PointNum) + 1000}= IFCCARTESIANPOINT(({NewPointCoord}));" + Environment.NewLine;
                File.AppendAllText(IFC_FilePath, InfoAboutPoint);
                //Counter1++;
            }
            bool IsContain(string CheckValue)
            {
                foreach (string str in PointsNumbers)
                {
                    if (str == CheckValue) return true;
                }
                return false;
            }
            //Заносим информацию о точках
            foreach (XElement OneFace in el_Faces.Elements())
            {
                string[] PointNums = OneFace.Value.Split(' ');
                for (int i1 = 0; i1 < 3; i1++)
                {
                    if (IsContain(PointNums[i1]) == false) AddInfoAboutPoint(PointNums[i1]);
                    if (Convert.ToInt64(PointNums[i1]) > NumMax) NumMax = Convert.ToInt64(PointNums[i1]);
                }
            }
            long Counter1 = 1000 + NumMax + 1;
            //Заносим информацию о гранях
            string IFCOPENSHELL = "#675= IFCOPENSHELL((";
            foreach (XElement OneFace in el_Faces.Elements())
            {
                string[] PointNums = OneFace.Value.Split(' ');
                string NewPointsNums =
                    "#" + (Convert.ToInt64(PointNums[0]) + 1000).ToString() + "," +
                    "#" + (Convert.ToInt64(PointNums[1]) + 1000).ToString() + "," +
                    "#" + (Convert.ToInt64(PointNums[2]) + 1000).ToString();

                string IFCPOLYLOOP = $"#{Counter1}= IFCPOLYLOOP(({NewPointsNums}));" + Environment.NewLine;
                string IFCFACEOUTERBOUND = $"#{Counter1 + 1}= IFCFACEOUTERBOUND(#{Counter1},.T.);" + Environment.NewLine;
                string IFCFACE = $"#{Counter1 + 2}= IFCFACE((#{Counter1 + 1}));" + Environment.NewLine;
                if (IFCOPENSHELL.Length < 22) IFCOPENSHELL += $"#{Counter1 + 2}";
                else IFCOPENSHELL += $",#{Counter1 + 2}";

                File.AppendAllText(IFC_FilePath, IFCPOLYLOOP + IFCFACEOUTERBOUND + IFCFACE);
                //File.AppendAllText(IFC_FilePath, InfoAboutPoint);
                Counter1 += 3;
            }
            //Добавляем блок информации о IFCOPENSHELL
            File.AppendAllText(IFC_FilePath, IFCOPENSHELL + "));" + Environment.NewLine);

            //Обязательная концовка файла
            File.AppendAllText(IFC_FilePath, "ENDSEC;" + Environment.NewLine + "END-ISO-10303-21;" + Environment.NewLine);
        }

    }
}
