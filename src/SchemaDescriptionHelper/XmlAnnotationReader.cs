using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Xml;
using SystemAttribute = System.Attribute;

namespace SchemaDescriptionHelper;

public class XmlAnnotationReader
{
    public string XmlPath { get; protected internal set; }
    public XmlDocument Document { get; protected internal set; }

    public XmlAnnotationReader()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = string.Format("{0}.{0}.XML", assembly.GetName().Name);
        this.XmlPath = resourceName;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        using var reader = new StreamReader(stream);
        var doc = new XmlDocument();
        doc.Load(reader);
        this.Document = doc;
    }

    public XmlAnnotationReader(string xmlPath)
    {
        this.XmlPath = xmlPath;
        if (File.Exists(xmlPath))
        {
            var doc = new XmlDocument();
            doc.Load(this.XmlPath);
            this.Document = doc;
        }
        else
        {
            throw new FileNotFoundException(
                $"Could not find the XmlDocument at the specified path: {xmlPath}\r\nCurrent Path: {Assembly.GetExecutingAssembly().Location}");
        }
    }

    /// <summary>
    /// 解析xml
    /// </summary>
    /// <returns></returns>
    public string GetCommentsForResource(string resourcePath, XmlResourceType type)
    {
        var node = Document.SelectSingleNode(
            $"//member[starts-with(@name, '{GetObjectTypeChar(type)}:{resourcePath}')]/summary");
        if (node == null)
        {
            return string.Empty;
        }

        var xmlResult = node.InnerText;
        var trimmedResult = Regex.Replace(xmlResult, @"\s+", " ");
        return trimmedResult;
    }

    /// <summary>
    /// 获取字段描述
    /// </summary>
    /// <returns></returns>
    public ObjectDocumentation[] GetCommentsForResource(Type objectType)
    {
        var comments = new List<ObjectDocumentation>();
        var resourcePath = objectType.FullName;
        var properties = objectType.GetProperties()
            .Where(x => (x.PropertyType.Namespace == nameof(System)
                         || x.PropertyType.Name == "Boolean"
                         || x.PropertyType.BaseType?.Name == "Enum")
                        && !SystemAttribute.IsDefined(x, typeof(NotMappedAttribute)))
            .ToArray();


        var objectNames = new List<ObjectDocumentation>();

        //属性
        objectNames.AddRange(properties.Select(x =>
            new ObjectDocumentation() { PropertyName = x.Name, Type = XmlResourceType.Property }).ToList());

        foreach (var property in objectNames)
        {
            // 判断列别名特性
            var customAttributes = properties.FirstOrDefault(x => x.Name == property.PropertyName)
                ?.GetCustomAttributes(typeof(ColumnAttribute), false);


            var node = Document.SelectSingleNode(
                $"//member[starts-with(@name, '{GetObjectTypeChar(property.Type)}:{resourcePath}.{property.PropertyName}')]/summary");
            if (node != null)
            {
                var xmlResult = node.InnerText;
                var trimmedResult = Regex.Replace(xmlResult, @"\s+", " ");
                property.Documentation = trimmedResult;
            }

            property.PropertyName = customAttributes != null && customAttributes.Length > 0
                ? (customAttributes[0] as ColumnAttribute)?.Name
                : property.PropertyName;

            property.Documentation = property.PropertyName switch
            {
                "Id" => "编号",
                "CreationTime" => "创建时间",
                "CreatorUserId" => "创建人Id",
                "LastModificationTime" => "最后更新时间",
                "LastModifierUserId" => "最后更新人",
                "IsDeleted" => "是否删除",
                "DeleterUserId" => "删除人",
                "DeletionTime" => "删除时间",
                "CreatorUserName" => "创建人",
                _ => property.Documentation
            };
            comments.Add(property);
        }

        return comments.ToArray();
    }

    /// <summary>
    /// 获取表描述
    /// </summary>
    /// <param name="objectType"></param>
    /// <param name="propertyName"></param>
    /// <param name="resourceType"></param>
    /// <returns></returns>
    public string GetCommentsForResource(Type objectType, string propertyName, XmlResourceType resourceType)
    {
        var comments = new List<ObjectDocumentation>();
        var resourcePath = objectType.FullName;

        var scopedElement = resourcePath;
        if (propertyName != null && resourceType != XmlResourceType.Type)
            scopedElement += "." + propertyName;
        var node = Document.SelectSingleNode(
            $"//member[starts-with(@name, '{GetObjectTypeChar(resourceType)}:{scopedElement}')]/summary");
        if (node == null)
        {
            return string.Empty;
        }

        var xmlResult = node.InnerText;
        var trimmedResult = Regex.Replace(xmlResult, @"\s+", " ");
        return trimmedResult;
    }

    /// <summary>
    /// 获取对象类型前缀
    /// </summary>
    /// <param name="type"></param>
    /// <returns></returns>
    private string GetObjectTypeChar(XmlResourceType type)
    {
        return type switch
        {
            XmlResourceType.Field => "F",
            XmlResourceType.Method => "M",
            XmlResourceType.Property => "P",
            XmlResourceType.Type => "T",
            _ => string.Empty
        };
    }
}