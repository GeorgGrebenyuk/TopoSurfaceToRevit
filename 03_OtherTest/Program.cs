using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace ConsoleApp8
{


    class Program
	{
		static void Main(string[] args)
		{
            //Console.WriteLine("Hello World!");
            GetPointsAndFacesFromLandxml(@"D:\GoogleCloud\Work\DsenArticles\Drawings\ZD_DemoSurface1.xml");
            Console.WriteLine("End");
            Console.ReadKey();
        }

        public static void GetPointsAndFacesFromLandxml(string PathToLandxml) //Dictionary <string,object>
        {
            XDocument SourceLandXml = XDocument.Load(PathToLandxml);
            System.Threading.Thread.CurrentThread.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;

            //IEnumerable<XYZ> Topo_Faces;
            //List<XYZ> Topo_Points = new List<XYZ>();
            //List<PolymeshFacet> Topo_Faces = new List<PolymeshFacet>();

            XElement el_Pnts = SourceLandXml.Descendants().Where(a => a.Name.LocalName == "Pnts").First();
            IEnumerable<XElement> el_PntsCollection = el_Pnts.Elements().Where(a => a.Name.LocalName == "P");
            double[][] All_Points = new double[el_PntsCollection.Count()][];



            XElement el_Faces = SourceLandXml.Descendants().Where(a => a.Name.LocalName == "Faces").First();
            IEnumerable<XElement> el_FacesCollection = el_Faces.Elements().Where(a => a.Name.LocalName == "F");

            long Counter1 = 0;
            foreach (var OnePoint in el_PntsCollection)
            {
                double [] GetCoordOfPoint = OnePoint.Value.Split(' ').Select(x => double.Parse(x, CultureInfo.GetCultureInfo("en-US"))).ToArray();
                All_Points[Counter1] = new double[3] {Math.Round(GetCoordOfPoint[0],3), Math.Round(GetCoordOfPoint[1], 3), Math.Round(GetCoordOfPoint[2], 3) };
                Counter1++;
            }

            Counter1 = 0;
            foreach (var OnePoint in All_Points)
			{

                if (IsThatPoint(OnePoint) == true)
				{
                    Console.WriteLine($"For x= {OnePoint[1]} and y= {OnePoint[0]} is exist clone point");
				}
                Counter1++;

            }

            bool IsThatPoint (double [] CoordsOfPoint)
			{
                long Counter2 = 0;
                foreach (var OnePoint in All_Points)
                {
                    if (OnePoint[2] != CoordsOfPoint [2] && OnePoint[0] == CoordsOfPoint[0] && OnePoint[1] == CoordsOfPoint[1]) return true;
                }
                Counter2++;
                return false;
            }
        }
    }
}
