using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Data.SqlClient;

namespace SchemaDescriptionHelper;

/// <summary>
/// 属性描述更新器
/// </summary>
/// <typeparam name="TContext"></typeparam>
public class SchemaDescriptionUpdater<TContext> where TContext : DbContext
{
    private Type contextType;
    private TContext context;
    private IDbContextTransaction transaction;
    private XmlAnnotationReader reader;

    /// <summary>
    /// DbContext
    /// </summary>
    /// <param name="context"></param>
    public SchemaDescriptionUpdater(TContext context)
    {
        this.context = context;
        reader = new XmlAnnotationReader();
    }
        
    /// <summary>
    /// 带xml路径
    /// </summary>
    /// <param name="context"></param>
    /// <param name="xmlDocumentationPath"></param>
    public SchemaDescriptionUpdater(TContext context, string xmlDocumentationPath)
    {
        this.context = context;
        reader = new XmlAnnotationReader(xmlDocumentationPath);
    }

    /// <summary>
    /// 更新数据库描述（主方法）
    /// </summary>
    public void UpdateDatabaseDescriptions()
    {
        contextType = typeof(TContext);
        var props = contextType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
        transaction = null;
        try
        {
            context.Database.OpenConnection();
            transaction = context.Database.BeginTransaction();
            foreach (var prop in props)
            {
                Console.WriteLine(prop.Name);
                if (!prop.PropertyType.InheritsOrImplements((typeof(DbSet<>))))
                {
                    continue;
                }

                var tableType = prop.PropertyType.GetGenericArguments()[0];

                SetTableDescriptions(tableType);

            }

            transaction.Commit();
        }
        catch
        {
            transaction?.Rollback();
            throw;
        }
        finally
        {
            context.Database.CloseConnection();

        }
    }

    #region private method


    /// <summary>
    /// 获取表名（实体名）
    /// </summary>
    /// <param name="tableType"></param>
    /// <returns></returns>
    private string GetTableName(Type tableType)
    {
        var tableName = tableType.Name;
        var customAttributes = tableType.GetCustomAttributes(typeof(TableAttribute), false);
        if (customAttributes.Any())
        {
            tableName = (customAttributes.First() as TableAttribute)?.Name;
        }

        return tableName;
    }

    /// <summary>
    /// 设置表名描述
    /// </summary>
    /// <param name="tableType"></param>
    private void SetTableDescriptions(Type tableType)
    {
        var fullTableName = GetTableName(tableType); //context.GetTableName(tableType);

        var regex = new Regex(@"(\[\w+\]\.)?\[(?<table>.*)\]");
        var match = regex.Match(fullTableName);
        var tableName = match.Success ? match.Groups["table"].Value : fullTableName;

        var tableAttrs = tableType.GetCustomAttributes(typeof(TableAttribute), false);
        if (tableAttrs.Length > 0)
        {
            tableName = ((TableAttribute)tableAttrs[0]).Name;
        }

        // 设置表的描述
        var tableComment = reader.GetCommentsForResource(tableType, null, XmlResourceType.Type);
        if (!string.IsNullOrEmpty(tableComment))
        {
            SetDescriptionForObject(tableName, null, tableComment);
        }

        // 所有列的描述
        var columnComments = reader.GetCommentsForResource(tableType);
        foreach (var column in columnComments)
        {
            SetDescriptionForObject(tableName, column.PropertyName, column.Documentation);
        }
    }

    /// <summary>
    /// 设置描述
    /// </summary>
    /// <param name="tableName"></param>
    /// <param name="columnName"></param>
    /// <param name="description"></param>
    private void SetDescriptionForObject(string tableName, string columnName, string description)
    {
        string strGetDesc;
        // 判断是否已经有描述
        if (string.IsNullOrEmpty(columnName))
            strGetDesc = "select top 1 CONVERT(varchar(max), [value]) from fn_listextendedproperty('MS_Description','schema','dbo','table',N'" + tableName + "',null,null)";
        else
            strGetDesc = "select top 1 CONVERT(varchar(max), [value]) from fn_listextendedproperty('MS_Description','schema','dbo','table',N'" + tableName + "','column',null) where objname = N'" + columnName + "'";
        var prevDesc = (string)RunSqlScalar(strGetDesc);

        var parameters = new List<SqlParameter>
        {
            new SqlParameter("@table", tableName),
            new SqlParameter("@desc", description)
        };

        // 更新or新建
        var funcName = "sp_addextendedproperty";
        if (!string.IsNullOrEmpty(prevDesc))
            funcName = "sp_updateextendedproperty";

        var query = @"EXEC " + funcName + @" @name = N'MS_Description', @value = @desc,@level0type = N'Schema', @level0name = 'dbo',@level1type = N'Table',  @level1name = @table";

        if (!string.IsNullOrEmpty(columnName))
        {
            parameters.Add(new SqlParameter("@column", columnName));
            query += ", @level2type = N'Column', @level2name = @column";
        }
        RunSql(query, parameters.ToArray());
    }

    /// <summary>
    /// 执行SQL
    /// </summary>
    /// <param name="cmdText"></param>
    /// <param name="parameters"></param>
    private void RunSql(string cmdText, params SqlParameter[] parameters)
    {
        // todo 可优化成批量执行语句
        context.Database.ExecuteSqlRaw(cmdText, parameters);
    }

    private object RunSqlScalar(string cmdText)
    {
        var resultParameter =
            new SqlParameter("@result", SqlDbType.VarChar) { Size = 2000, Direction = ParameterDirection.Output };

        context.Database.ExecuteSqlRaw($"set @result = ({cmdText})", resultParameter);
        return resultParameter.Value as string;
    }

    #endregion

}