﻿using System.Data.SQLite;
using System.IO;
using Dapper;

namespace Web.Admin.Data
{
    public class SqLiteBaseRepository
    {
        protected string DbFile;

        public SQLiteConnection SimpleDbConnection()
        {
            return new SQLiteConnection("Data Source=" + DbFile);
        }

        public SqLiteBaseRepository(string dataFolderPath)
        {
            if (!System.IO.Directory.Exists(dataFolderPath))
            {
                Directory.CreateDirectory(dataFolderPath);
            }
            DbFile = Path.Combine(dataFolderPath, "SimpleDb.sqlite");
            if (!File.Exists(DbFile))
            {
                CreateDatabase();
            }
        }

        private void CreateDatabase()
        {
            using (var cnn = SimpleDbConnection())
            {
                cnn.Open();
                cnn.Execute(
                    @"create table MapPoints
                      (
                         ID                           integer primary key AUTOINCREMENT,
                         TweetId                      varchar(100) not null,
                         Text                         nvarchar(300) not null,
                         TweetUrl                     varchar(500) not null,
                         Img                          varchar(500) not null,
                         TwitterHandle                varchar(100) not null,
                         LocationConfidence           varchar(100) not null,
                         CreatedDate                  datetime not null,
                         Geo_x                        real not null,
                         Geo_y                        real not null,
                         Area                         varchar(100) not null,
                         Hide                         integer not null
                      )");
            }
        }
    }
}
