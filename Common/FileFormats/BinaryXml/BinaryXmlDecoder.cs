using System;
using System.IO;
using System.Text;
using System.Xml;

namespace monono2.Common.BinaryXml
{
    /*
     * Code was converted from Aion Exporter
     * 
     * XML generation: http://www.genedavis.com/library/xml/java_dom_xml_creation.jsp
     */

    public class BinaryXmlDecoder
    {
        private static void WriteNode(XmlWriter xw, BinaryXmlNode node)
        {
            xw.WriteStartElement(node.Name);
            foreach (var kvp in node.Attributes)
                xw.WriteAttributeString(kvp.Key, kvp.Value);

            foreach (BinaryXmlNode node1 in node.Children)
                WriteNode(xw, node1);

            if (node.Value != null)
                xw.WriteValue(node.Value);
            xw.WriteEndElement();
        }

        public static Stream Decode(BinaryReader input, bool includeBom)
        {
            var binaryXmlFile = new BinaryXmlFile();
            binaryXmlFile.Read(input);

            // Creating an empty XML Document
            //DocumentBuilderFactory dbfac = DocumentBuilderFactory.newInstance();
            //DocumentBuilder docBuilder = dbfac.newDocumentBuilder();
            //Document doc = docBuilder.newDocument();

            var settings = new XmlWriterSettings();
            settings.Encoding = new UnicodeEncoding(false, includeBom);
            settings.NewLineHandling = NewLineHandling.Entitize;
            settings.Indent = true;

            // use a MemoryStream instead of string to preserve encoding.
            var ms = new MemoryStream();
            using (var xw = XmlWriter.Create(ms, settings))
            {
                xw.WriteStartDocument();
                WriteNode(xw, binaryXmlFile.Root);
                xw.WriteEndDocument();
                xw.Close();
            }
                
            // Output the XML
            ms.Position = 0;
            return ms;

            //// set up a transformer
            //TransformerFactory transfac = TransformerFactory.newInstance();
            //Transformer trans = transfac.newTransformer();
            //trans.setOutputProperty(OutputKeys.OMIT_XML_DECLARATION, "yes");
            //trans.setOutputProperty(OutputKeys.INDENT, "yes");
            //trans.setOutputProperty(OutputKeys.ENCODING, "Unicode");

            //// write XML tree to the stream
            //StreamResult result = new StreamResult(output);
            //DOMSource source = new DOMSource(doc);
            //trans.transform(source, result);
        }
    }
}
