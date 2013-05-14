package ime.mail.xml;

import org.w3c.dom.*;
import org.apache.xpath.XPathAPI;
import org.directwebremoting.util.Logger;

import java.io.*;
import java.util.*;
import javax.xml.transform.TransformerException;

/**
 * This class contains some static methods that are used commonly in other WebMail parts.
 *
 * @author Sebastian Schaffert
 */
public final class XMLCommon  {
    private static Logger log = Logger.getLogger(XMLCommon.class);
    
    static String getParentXPath(String str) {
        int last_slash = str.lastIndexOf("/");
        if(last_slash == -1) {
            return ".";
        } else {
            return str.substring(0,last_slash);
        }
    }


    /**
     * Return the node value of a single node selected by the given xpath
     * expression.
     */
    public static String getValueXPath(Element root, String path) {
        root.normalize();
        try {
            Node n = XPathAPI.selectSingleNode(root,path);
            if(n != null) {
                return n.getNodeValue();
            } else {
                return null;
            }
        } catch(Exception ex) {
            log.error("Xpath query failed.  Continuing as if no node found.",
                    ex);
            return null;
        }
    }

    /**
     * Set the node value of the node selected by the given xpath expression.
     */
    public static void setValueXPath(Element root, String path, String value) {
        root.normalize();
        try {
            Node n = XPathAPI.selectSingleNode(root,path);
            if(n != null) {
                n.setNodeValue(value);
            } else {
                addNodeXPath(root,getParentXPath(path),root.getOwnerDocument().createTextNode(value));
            }
        } catch(TransformerException ex) {
            addNodeXPath(root,getParentXPath(path),root.getOwnerDocument().createTextNode(value));
        } catch(Exception ex) {
            log.error("Failed to set value '" + value + "' for path '"
                    + path + "'.  Continuing, but should not.", ex);
            // TODO:  Throw here.
        }
    }


    public static Node getNodeXPath(Element root, String path) {
        try {
            Node n = XPathAPI.selectSingleNode(root,path);
            return n;
        } catch(Exception ex) {
            log.error("XPath query '" + path
                    + "' failed, but behaving as if node not found", ex);
            // Can easily dumpXML here if that is useful.
            // TODO:  Throw here.
        }
        return null;
    }

    /**
     * Add a node as child to the node selected by the given xpath expression.
     */
    public static void addNodeXPath(Element root, String path, Node child) {
        try {
            Node n = XPathAPI.selectSingleNode(root,path);
            n.appendChild(child);
        } catch(Exception ex) {
            log.error("Failed to add nod for path '"
                    + path + "'.  Continuing, but should not.", ex);
            // TODO:  Throw here.
        }
    }


    public static NodeList getNodeListXPath(Element root, String path) {
        try {
            NodeList n = XPathAPI.selectNodeList(root,path);
            return n;
        } catch(Exception ex) {
            log.error("XPath query '" + path
                    + "' failed, but behaving as if no nodes found", ex);
            // Can easily dumpXML here if that is useful.
            // TODO:  Throw here.
        }
        return null;
    }


    /**
     * @deprecated use getXPath instead!
     */
    @Deprecated
    public static Element getElementByAttribute(Element root, String tagname, String attribute, String att_value) {
        NodeList nl=root.getElementsByTagName(tagname);
        for(int i=0; i<nl.getLength();i++) {
            Element elem=(Element)nl.item(i);
            if(elem.getAttribute(attribute).equals(att_value)) {
                return elem;
            }
        }
        return null;
    }


    /**
     * @deprecated use getXPath instead!
     */
    @Deprecated
    public static String getElementTextValue(Element e) {
        e.normalize();
        NodeList nl=e.getChildNodes();
        if(nl.getLength() <= 0) {
            log.error("Elements: "+nl.getLength());
            // Should we not throw here so us developers will see and
            // fix the problem? - blaine
            return "";
        } else {
            String s="";
            for(int i=0;i<nl.getLength();i++) {
                if(nl.item(i) instanceof CharacterData) {
                    s+=nl.item(i).getNodeValue();
                }
            }
            return s.trim();
        }
    }

    public static void setElementTextValue(Element e,String text) {
        setElementTextValue(e,text,false);
    }

    public static void setElementTextValue(Element e,String text, boolean cdata) {
        Document root=e.getOwnerDocument();
        e.normalize();
        if(e.hasChildNodes()) {
            NodeList nl=e.getChildNodes();

            /* This suxx: NodeList Object is changed when removing children !!!
               I will store all nodes that should be deleted in a Vector and delete them afterwards */
            int length=nl.getLength();

            List<Node> v = new ArrayList<Node>(nl.getLength());
            for(int i=0;i<length;i++)
                if(nl.item(i) instanceof CharacterData) v.add(nl.item(i));
            for (Node n : v) e.removeChild(n);
        }

        if(cdata) {
            e.appendChild(root.createCDATASection(text));
        } else {
            e.appendChild(root.createTextNode(text));
        }
    }

    /**
     * @deprecated use getXPath instead!
     */
    @Deprecated
    public static String getTagValue(Element e, String tagname) {
        NodeList namel=e.getElementsByTagName(tagname);
        if(namel.getLength()>0) {
            return getElementTextValue((Element)namel.item(0));
        } else {
            return null;
        }
    }

    /**
     * Set the value of the first tag below e with name tagname to text.
     */
    public static void setTagValue(Element e,String tagname, String text) {
        setTagValue(e,tagname,text,false);
    }

    public static void setTagValue(Element e,String tagname, String text,boolean cdata) {
        try {
            setTagValue(e,tagname,text,false,"",cdata);
        } catch(Exception ex) {}
    }

    public static void setTagValue(Element e,String tagname, String text,
                                      boolean unique,String errormsg) throws Exception
    {
        setTagValue(e,tagname,text,unique,errormsg,false);
    }

    public static void setTagValue(Element e,String tagname, String text,
                                   boolean unique,String errormsg, boolean cdata)
        throws Exception {
        if(text == null || tagname == null) throw new NullPointerException("Text or Tagname may not be null!");

        Document root=e.getOwnerDocument();

        if(unique) {
            // Check for double entries!
            NodeList nl=((Element)e.getParentNode()).getElementsByTagName(tagname);
            for(int i=0;i<nl.getLength();i++) {
                if(getElementTextValue((Element)nl.item(0)).equals(text)) {
                    throw new Exception(errormsg);
                }
            }
        }
        NodeList namel=e.getElementsByTagName(tagname);
        Element elem;
        if(namel.getLength()<=0) {
            log.debug("Creating Element "+tagname+"; will set to "+text);
            elem=root.createElement(tagname);
            e.appendChild(elem);
        } else {
            elem=(Element)namel.item(0);
        }
        //debugXML(root);
        setElementTextValue(elem,text,cdata);
    }

    public static void genericRemoveAll(Element parent, String tagname) {
        NodeList nl=parent.getChildNodes();
        List<Element> parts = new ArrayList<Element>();
        for(int i=0;i<nl.getLength();i++) {
            if(nl.item(i) instanceof Element) {
                Element elem=(Element)nl.item(i);
                if(elem.getTagName().equals(tagname))
                    parts.add(elem);
            }
        }
        for (Element part : parts) parent.removeChild(part);
    }


    public static void writeXML(Document d, OutputStream os, String sysID) throws IOException {
        /**
         * To support i18n, we have to specify the encoding of
         * output writer to UTF-8 when we writing the XML.
         */
        // PrintWriter out=new PrintWriter(os);
        PrintWriter out = new PrintWriter(new OutputStreamWriter(os, "UTF-8"));

        out.println("<?xml version=\"1.0\" encoding=\"UTF-8\"?>");
        out.println();
        if(sysID != null) {
            out.println("<!DOCTYPE "+d.getDoctype().getName()+" SYSTEM \""+sysID+"\">");
            out.println();
        }
        //d.getDocumentElement().normalize();
        writeXMLwalkTree(d.getDocumentElement(),0,out);
        out.flush();
    }

    protected static void writeXMLwalkTree(Node node, int indent, PrintWriter out) {
        if (node == null) throw new NullPointerException(
                "Null node passed to writeXMLwalkTree()");
        if(node.hasChildNodes()) {
            if(node instanceof Element) {
                Element elem=(Element)node;
                //elem.normalize();
                out.print("\n");
                for(int j=0;j<indent;j++) {
                    out.print(" ");
                }
                out.print("<"+elem.getTagName());
                NamedNodeMap attrs=elem.getAttributes();
                for(int i=0;i<attrs.getLength();i++) {
                    Attr a=(Attr)attrs.item(i);
                    out.print(" "+a.getName()+"=\""+a.getValue()+"\"");
                }
                out.print(">");
                NodeList nl=node.getChildNodes();
                for(int i=0;i<nl.getLength();i++) {
                    writeXMLwalkTree(nl.item(i),indent+2,out);
                }
//              for(int j=0;j<indent;j++) {
//                  out.print(" ");
//              }
                out.println("</"+elem.getTagName()+">");
            }
        } else {
            if(node instanceof Element) {
                Element elem=(Element)node;
                out.print("\n");
                for(int j=0;j<indent;j++) {
                    out.print(" ");
                }
                out.print("<"+elem.getTagName());
                NamedNodeMap attrs=elem.getAttributes();
                for(int i=0;i<attrs.getLength();i++) {
                    Attr a=(Attr)attrs.item(i);
                    out.print(" "+a.getName()+"=\""+a.getValue()+"\"");
                }
                out.println("/>");
            } else if(node instanceof CDATASection) {
                CDATASection cdata=(CDATASection)node;
//              for(int j=0;j<indent;j++) {
//                  out.print(" ");
//              }
                out.print("<![CDATA["+cdata.getData()+"]]>");
            } else if(node instanceof Text) {
                Text text=(Text)node;
                StringBuilder buf=new StringBuilder(text.getData().length());
                for(int i=0;i<text.getData().length();i++) {
                    if(text.getData().charAt(i) == '\n' ||
                       text.getData().charAt(i) == '\r' ||
                       text.getData().charAt(i) == ' ' ||
                       text.getData().charAt(i) == '\t') {
                        if(buf.length()>0 && buf.charAt(buf.length()-1)!=' ') {
                            buf.append(' ');
                        }
                    } else {
                        buf.append(text.getData().charAt(i));
                    }
                }
                if(buf.length() > 0 && !buf.toString().equals(" ")) {
                    StringBuilder buf2=new StringBuilder(buf.length()+indent);
//                  for(int j=0;j<indent;j++) {
//                      buf2.append(' ');
//                  }
                    buf2.append(buf.toString());
                    out.print(buf2);
                }
            }
        }
    }

    /**
     * This is a helper function to deal with problems that occur when importing Nodes from
     * JTidy Documents to Xerces Documents.
     */
    public static Node importNode(Document d, Node n, boolean deep) {
        Node r=cloneNode(d,n);
        if(deep) {
            NodeList nl=n.getChildNodes();
            for(int i=0;i<nl.getLength();i++) {
                Node n1=importNode(d,nl.item(i),deep);
                r.appendChild(n1);
            }
        }
        return r;
    }

    public static Node cloneNode(Document d,Node n) {
        Node r=null;
        switch(n.getNodeType()) {
        case Node.TEXT_NODE:
            r = d.createTextNode(((Text)n).getData());
            break;
        case Node.CDATA_SECTION_NODE:
            r = d.createCDATASection(((CDATASection)n).getData());
            break;
        case Node.ELEMENT_NODE:
            r = d.createElement(((Element)n).getTagName());
            NamedNodeMap map=n.getAttributes();
            for(int i=0;i<map.getLength();i++) {
                ((Element)r).setAttribute(((Attr)map.item(i)).getName(),
                                          ((Attr)map.item(i)).getValue());
            }
            break;
        }
        return r;
    }
}
