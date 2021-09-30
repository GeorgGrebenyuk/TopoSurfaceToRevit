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
       // private static List<XYZ> Topo_Points = new List<XYZ>();
        //private static List<PolymeshFacet> Topo_Faces = new List<PolymeshFacet>();
        private MainClass () //List<XYZ> Group1, List<PolymeshFacet> Group2
        {
            //this.Topo_P = Group1;
			//this.Topo_F = Group2;
        }
        public static void CreateTopo (List<XYZ> Topo_Points, List<PolymeshFacet> Topo_Faces)
		{
            var RevitDoc = RevitServices.Persistence.DocumentManager.Instance.CurrentDBDocument;

            //GetPointsAndFacesFromLandxml(PathToLandxml);

            Transaction trans = new Transaction(RevitDoc);
            trans.Start("Create topo surface");
            Autodesk.Revit.DB.Architecture.TopographySurface.Create(RevitDoc, Topo_Points, Topo_Faces);
            trans.Commit();


        }

        [MultiReturn(new[] { "Topo_Points", "Topo_Faces" })]
        public static Dictionary<string, object> GetPointsAndFacesFromLandxml (string PathToLandxml) //Dictionary <string,object>
        {
            XDocument SourceLandXml = XDocument.Load(PathToLandxml);
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
            List<XYZ> Topo_Points = new List<XYZ>();
            List<PolymeshFacet> Topo_Faces = new List<PolymeshFacet>();

        //IEnumerable<XYZ> Topo_Points;
        //IEnumerable<XYZ> Topo_Faces;
        //List<XYZ> Topo_Points = new List<XYZ>();
        //List<PolymeshFacet> Topo_Faces = new List<PolymeshFacet>();

        XElement el_Pnts = SourceLandXml.Descendants().Where(a => a.Name.LocalName == "Pnts").First();
            IEnumerable<XElement> el_PntsCollection = el_Pnts.Elements().Where(a => a.Name.LocalName == "P");

            XElement el_Faces = SourceLandXml.Descendants().Where(a => a.Name.LocalName == "Faces").First();
            IEnumerable<XElement> el_FacesCollection = el_Faces.Elements().Where(a => a.Name.LocalName == "F");

            foreach (var OnePoint in el_PntsCollection)
			{
                double[] PntsCoords = OnePoint.Value.Split(' ').Select(x => double.Parse(x, CultureInfo.GetCultureInfo("en-US"))).ToArray();
                XYZ new_Point = new XYZ(PntsCoords[1], PntsCoords[0], PntsCoords[2]);
                Topo_Points.Add(new_Point);
            }
            foreach (var OneFace in el_FacesCollection)
			{
                int [] PntsNums = OneFace.Attribute("n").Value.Split(' ').Select(x => int.Parse(x, CultureInfo.GetCultureInfo("en-US"))).ToArray();
                PolymeshFacet new_Face =  new PolymeshFacet(PntsNums[0], PntsNums[1], PntsNums[2]);
                Topo_Faces.Add(new_Face);
            }

			return new Dictionary<string, object>()
			{
                {"Topo_Points",Topo_Points},
                {"Topo_Faces", Topo_Faces},
            };
		}

    }
}
