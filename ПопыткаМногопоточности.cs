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
using System.Threading;


namespace CreateTopoTest
{
    public class MainClass
    {
        private MainClass () //List<XYZ> Group1, List<PolymeshFacet> Group2
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
        public static void CreateTopo (List<XYZ> Topo_Points, List<PolymeshFacet> Topo_Faces)
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
        public static double [] GetBasePointCoords ()
		{
            double[] BP = new double[3];
            var RevitDoc = RevitServices.Persistence.DocumentManager.Instance.CurrentDBDocument;
            Transaction trans = new Transaction(RevitDoc);
            trans.Start("Get Base (survey) point coordinates");
            var basePoint = new FilteredElementCollector(RevitDoc).OfClass(typeof(BasePoint)).Cast<BasePoint>().Where(p => p.IsShared).ToList().FirstOrDefault();

            BP_Y= basePoint.get_Parameter(BuiltInParameter.BASEPOINT_NORTHSOUTH_PARAM).AsDouble();
            BP_X = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_EASTWEST_PARAM).AsDouble();
            BP_Z = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_ELEVATION_PARAM).AsDouble();
            BP[0] = BP_X; BP[1] = BP_Y; BP[2] = BP_Z;
            //BasePoint_Angle = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_ANGLETON_PARAM).AsDouble();

            trans.Commit();
            return BP;
        }
        private static XDocument SourceLandXml;
        private static ds_geom.Point Min_point;
        private static ds_geom.Point Max_point;
        private static  List<PolymeshFacet> Topo_Faces;
        private static List<XYZ> Topo_Points = new List<XYZ>();
        private static List<XYZ> Topo_Points_temp = new List<XYZ>();
        private static List<XElement> el_PntsCollection;
        private static List<XElement> el_FacesCollection;
        private static string[] IndexesOdPoints;

        /// <summary>
        /// Node that convert landxml's data to Lists with Points and faces
        /// </summary>
        /// <param name="PathToLandxml">Absolute file's path to landxml file</param>
        /// <param name="UnitsCoeff">Auxilary double-value coefficient (between meters in landxml surface definition to Revit's units). Optionally = 1000 </param>
        /// <param name="Min_point">Minimum point - left bottom in internal Revit's coordinates</param>
        /// <param name="Max_point">Maximum point - upper right in internal Revit's coordinates</param>
        /// <returns></returns>
        [MultiReturn(new[] { "Topo_Points", "Topo_Faces" })]
        public static Dictionary<string, object> GetPointsAndFacesFromLandxml (string PathToLandxml,  ds_geom.Point Min_point_input, ds_geom.Point Max_point_input, ds_geom.Point RevitsBasePoint, bool UseAllSurface = false, double UnitsCoeff = 1d/0.3048) //Dictionary <string,object>
        {
            Min_point = Min_point_input;
            Max_point = Max_point_input;
            //GetBasePointCoords();
            BP_X = RevitsBasePoint.X;
            BP_Y = RevitsBasePoint.Y;
            BP_Z = RevitsBasePoint.Z;

            //XDocument SourceLandXml = XDocument.Load(PathToLandxml);
            SourceLandXml = XDocument.Load(PathToLandxml);
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            //List<PolymeshFacet> Topo_Faces = new List<PolymeshFacet>();

            XElement el_Pnts = SourceLandXml.Descendants().Where(a => a.Name.LocalName == "Pnts").First();
            el_PntsCollection = el_Pnts.Elements().Where(a => a.Name.LocalName == "P").ToList();

            XElement el_Faces = SourceLandXml.Descendants().Where(a => a.Name.LocalName == "Faces").First();
            el_FacesCollection = el_Faces.Elements().Where(a => a.Name.LocalName == "F").ToList();

            IndexesOdPoints = new string [el_PntsCollection.Count()];
            void AddPntsToList (List<XYZ> ToSave)
            {
                int i1 = 0;
                foreach (var OnePoint in el_PntsCollection)
                {
                    double[] PntsCoords = OnePoint.Value.Split(' ').Select(x => double.Parse(x, CultureInfo.GetCultureInfo("en-US")) * UnitsCoeff).ToArray();
                    XYZ new_Point = new XYZ(PntsCoords[1] - BP_X, PntsCoords[0]- BP_Y, PntsCoords[2] - BP_Z);
                        ToSave.Add(new_Point);
                    IndexesOdPoints[i1] = OnePoint.Attribute("id").Value;
                    i1++;
                }
            }

            if (UseAllSurface == true)
            {
                AddPntsToList(Topo_Points);

                foreach (var OneFace in el_FacesCollection)
                {
                    int[] PntsNums = OneFace.Value.Split(' ').Select(x => int.Parse(x, CultureInfo.GetCultureInfo("en-US"))).ToArray();
                    PolymeshFacet new_Face = new PolymeshFacet(GetIndexOfFacetsPoint(PntsNums[0]), GetIndexOfFacetsPoint(PntsNums[1]), GetIndexOfFacetsPoint(PntsNums[2]));
                    Topo_Faces.Add(new_Face);
                }
            }
            else
            {

                AddPntsToList(Topo_Points_temp);
                int LenOfFaces = el_FacesCollection.Count;
                int Counter = Convert.ToInt32(LenOfFaces / 4);
                //Первый поток
                SortData Process_1 = new SortData(0, Counter);
                Thread NewIteration_Process_1 = new Thread(new ThreadStart(Process_1.SortPartOfCollection)); NewIteration_Process_1.Start();
                //Второй поток
                SortData Process_2 = new SortData(Counter, Counter*2);
                Thread NewIteration_Process_2 = new Thread(new ThreadStart(Process_2.SortPartOfCollection)); NewIteration_Process_2.Start();
                //Третий поток
                SortData Process_3 = new SortData(Counter*2, Counter*3);
                Thread NewIteration_Process_3 = new Thread(new ThreadStart(Process_3.SortPartOfCollection)); NewIteration_Process_3.Start();
                //Четвертый поток
                SortData Process_4 = new SortData(Counter*3, LenOfFaces);
                Thread NewIteration_Process_4 = new Thread(new ThreadStart(Process_4.SortPartOfCollection)); NewIteration_Process_4.Start();

                NewIteration_Process_1.Join(); NewIteration_Process_2.Join(); NewIteration_Process_3.Join(); NewIteration_Process_4.Join();
            }

			return new Dictionary<string, object>()
			{
                {"Topo_Points",Topo_Points},
                {"Topo_Faces", Topo_Faces},
            };
		}
        public class SortData
        {
            public SortData(int i1, int i2)
            {
                this.StartCounter = i1;
                this.StartCounter = i2;
            }
            private int StartCounter;
            private int EndCounter;
            public void SortPartOfCollection()
            {
                int i3 = 0;
                for (int i1 = StartCounter; i1 < EndCounter; i1++)
                {
                    var OneFace = el_FacesCollection[i1];
                    int[] PntsNums = OneFace.Value.Split(' ').Select(x => int.Parse(x, CultureInfo.GetCultureInfo("en-US"))).ToArray();
                    bool IsThatFaceNeeding = false;
                    foreach (int OneIndexPoint in PntsNums)
                    {
                        XYZ that_pnt = Topo_Points_temp[GetIndexOfFacetsPoint(OneIndexPoint)];
                        //XYZ that_pnt = new XYZ(that_pnt_coords[1] - BP_X, that_pnt_coords[0] - BP_Y, that_pnt_coords[2] - BP_Z);
                        //double that_pnt_X = that_pnt[1] - BP_X; double that_pnt_Y = that_pnt[0] - BP_Y;
                        if (that_pnt.X >= Min_point.X && that_pnt.Y >= Min_point.Y && that_pnt.X <= Max_point.X && that_pnt.Y <= Max_point.Y)
                        {
                            IsThatFaceNeeding = true;
                            break;
                        }
                    }
                    if (IsThatFaceNeeding == true)
                    {
                        lock (Topo_Points)
                        {
                            List<int> pnt_indexes = new List<int>();
                            foreach (int OneIndexPoint in PntsNums)
                            {
                                XYZ that_pnt = Topo_Points_temp[GetIndexOfFacetsPoint(OneIndexPoint)];
                                int index_of_pnt;
                                if (Topo_Points.Contains(that_pnt))
                                {
                                    index_of_pnt = Topo_Points.IndexOf(that_pnt);
                                }
                                else
                                {
                                    Topo_Points.Add(that_pnt);
                                    index_of_pnt = i3;
                                    i3++;
                                }
                                pnt_indexes.Add(index_of_pnt);
                            }
                            PolymeshFacet new_Face = new PolymeshFacet(pnt_indexes[0], pnt_indexes[1], pnt_indexes[2]);
                            Topo_Faces.Add(new_Face);
                        }
                        

                    }
                }
            }
        }
        
        //Auxilay method that return true Point's position by point's id in Face's definition
        private static int GetIndexOfFacetsPoint(int CurrentIndexInFace)
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

    }
}
