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
        private static void GetBasePointCoords ()
		{
            var RevitDoc = RevitServices.Persistence.DocumentManager.Instance.CurrentDBDocument;
            Transaction trans = new Transaction(RevitDoc);
            trans.Start("Get Base (survey) point coordinates");
            var basePoint = new FilteredElementCollector(RevitDoc).OfClass(typeof(BasePoint)).Cast<BasePoint>().Where(p => p.IsShared).ToList().FirstOrDefault();

            BP_Y= basePoint.get_Parameter(BuiltInParameter.BASEPOINT_NORTHSOUTH_PARAM).AsDouble();
            BP_X = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_EASTWEST_PARAM).AsDouble();
            BP_Z = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_ELEVATION_PARAM).AsDouble();
            //BasePoint_Angle = basePoint.get_Parameter(BuiltInParameter.BASEPOINT_ANGLETON_PARAM).AsDouble();

            trans.Commit();
        }
        /// <summary>
        /// Node that convert landxml's data to Lists with Points and faces
        /// </summary>
        /// <param name="PathToLandxml">Absolute file's path to landxml file</param>
        /// <param name="UnitsCoeff">Auxilary double-value coefficient (between meters in landxml surface definition to Revit's units). Optionally = 1000 </param>
        /// <returns></returns>
        [MultiReturn(new[] { "Topo_Points", "Topo_Faces" })]
        public static Dictionary<string, object> GetPointsAndFacesFromLandxml (string PathToLandxml, double UnitsCoeff = 1d/0.3048) //Dictionary <string,object>
        {
            GetBasePointCoords();

            XDocument SourceLandXml = XDocument.Load(PathToLandxml);
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            List<XYZ> Topo_Points = new List<XYZ>();
            List<PolymeshFacet> Topo_Faces = new List<PolymeshFacet>();

            XElement el_Pnts = SourceLandXml.Descendants().Where(a => a.Name.LocalName == "Pnts").First();
            IEnumerable<XElement> el_PntsCollection = el_Pnts.Elements().Where(a => a.Name.LocalName == "P");

            XElement el_Faces = SourceLandXml.Descendants().Where(a => a.Name.LocalName == "Faces").First();
            IEnumerable<XElement> el_FacesCollection = el_Faces.Elements().Where(a => a.Name.LocalName == "F");

            string [] IndexesOdPoints = new string [el_PntsCollection.Count()];

            int i1 = 0;
            foreach (var OnePoint in el_PntsCollection)
			{
                double[] PntsCoords = OnePoint.Value.Split(' ').Select(x => double.Parse(x, CultureInfo.GetCultureInfo("en-US")) * UnitsCoeff).ToArray();
                XYZ new_Point = new XYZ(PntsCoords[1] - BP_X, PntsCoords[0] - BP_Y, PntsCoords[2] - BP_Z);
                Topo_Points.Add(new_Point);
                IndexesOdPoints[i1] =  OnePoint.Attribute("id").Value;
                i1++;
            }
            foreach (var OneFace in el_FacesCollection)
			{
                int[] PntsNums = OneFace.Value.Split(' ').Select(x => int.Parse(x, CultureInfo.GetCultureInfo("en-US"))).ToArray();
                PolymeshFacet new_Face =  new PolymeshFacet(GetIndexOfFacetsPoint(PntsNums[0]), GetIndexOfFacetsPoint(PntsNums[1]), GetIndexOfFacetsPoint(PntsNums[2]));
                Topo_Faces.Add(new_Face);
            }

            //Auxilay method that return true Point's position by point's id in Face's definition
            int GetIndexOfFacetsPoint (int CurrentIndexInFace)
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
            };
		}

    }
}
