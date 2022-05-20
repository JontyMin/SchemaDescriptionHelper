# SchemaDescriptionHelper
从实体注释中获取字段描述并更新到SQL Server数据字段描述中

> 使用前提：EF Core

1、首先生成实体类所在项目的xml文件

右键项目属性：

<img src="https://cdn.jonty.top/img/image-20220520155957180.png" alt="image-20220520155957180" style="zoom:67%;" />

2、在你想要执行的地方调用生成方法

```csharp
#if DEBUG
            #region 生成描述

            var projectName = "your project.Core";

            var xmlPath =
                $@"{Environment.CurrentDirectory.Substring(0, Environment.CurrentDirectory.LastIndexOf(@"src", StringComparison.Ordinal))}src\{projectName}\{projectName}.xml";
            var updater = new SchemaDescriptionUpdater<YourDbContext>(context: CreateDbContext(), xmlPath);
            updater.UpdateDatabaseDescriptions();

            #endregion 生成描述

#endif
```

