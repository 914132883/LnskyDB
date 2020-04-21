using LnskyDB;
using LnskyDB.Test.MsSql.Entity.Data;
using LnskyDB.Test.MsSql.Entity.Purify;
using LnskyDB.Test.MsSql.Repository.Purify;
using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using System;
using System.Linq;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.IO;
using LnskyDB.Model;

namespace LnskyDB.Test
{
    public class MsSqlTest
    {

        public static object lockObj = new object();
        static List<string> lstDataSource = new List<string> { "������Դ1", "������Դ2", "��Դ�Զ�����" };

        static Dictionary<Guid, string> dicProduct = new Dictionary<Guid, string>();

        [SetUp]
        public void Setup()
        {
            var configuration = new ConfigurationBuilder().SetBasePath(Directory.GetCurrentDirectory()).AddJsonFile("appsettings.json").Build();
            DBTool.Configuration = configuration;
            DBTool.Error += DBTool_Error;
            InitDic(dicProduct, "������Ʒ", 10);
            DBTool.BeginThread();
            Index();
        }

        private void DBTool_Error(LnskyDB.Model.LnskyDBErrorArgs e)
        {
            Console.WriteLine(e.LogInfo);
        }

        private static void InitDic(Dictionary<Guid, string> dic, string namePre, int count)
        {
            for (int i = 1; i < count + 1; i++)
            {
                dic.Add(Guid.NewGuid(), namePre + i);
            }
        }
        public string Index()
        {
            if (isRuning == true)
            {
                return "����������";
            }
            lock (lockObj)
            {

                isRuning = true;
                try
                {
                    using (var tran = DBTool.BeginTransaction())
                    {
                        var repositoryFactory = RepositoryFactory.Create<ProductSaleByDayEntity>();
                        var query = QueryFactory.Create<ProductSaleByDayEntity>(m => m.CreateDate > DateTime.Now.Date);
                        query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 09, 01);
                        if (repositoryFactory.GetList(query).Count > 10)
                        {
                            return "";
                        }
                        var importGroupId = Guid.NewGuid();
                        var random = new Random();

                        var tempDate = new DateTime(2018, 1, 1);
                        while (tempDate <= new DateTime(2019, 12, 31))
                        {
                            if (tempDate.Day == 1)
                            {
                                query = QueryFactory.Create<ProductSaleByDayEntity>();
                                query.DBModel.DBModel_ShuffledTempDate = tempDate;
                                repositoryFactory.Delete(query);
                            }
                            foreach (var p in dicProduct)
                            {
                                var temp = new ProductSaleByDayEntity();

                                temp.SysNo = Guid.NewGuid();
                                temp.DataSource = lstDataSource[random.Next(lstDataSource.Count)];
                                temp.ShopName = "���Ե���";
                                temp.ProductID = p.Key;
                                temp.OutProductID = p.Value;
                                temp.ProductName = p.Value;
                                temp.Sales = random.Next(100000);
                                temp.StatisticalDate = tempDate;
                                temp.UpdateDate = temp.CreateDate = DateTime.Now;
                                temp.UpdateUserID = temp.CreateUserID = Guid.NewGuid();
                                temp.ImportGroupId = importGroupId;
                                repositoryFactory.Add(temp);

                            }
                            tempDate = tempDate.AddDays(1);
                        }
                        tran.Complete();
                    }
                }
                finally
                {

                    isRuning = false;
                }
                return "��ʼ���ɹ�";
            }

        }
        static bool isRuning = false;
        static int i = 0;
        private static IRepository<ProductSaleByDayEntity> GetRepository()
        {

            i++;
            if (i % 2 == 0)
            {
                //����ͨ����������
                return RepositoryFactory.Create<ProductSaleByDayEntity>();
            }
            else
            {
                //Ҳ���Լ̳�ʵ����
                return new ProductSaleByDayRepository();
            }
        }
        [Test]
        public void TestProductSaleByDayGet()
        {
            TestProductSaleByDayGet(new DateTime(2019, 01, 01));
            TestProductSaleByDayGet(new DateTime(2018, 02, 01));
        }

        private static void TestProductSaleByDayGet(DateTime dt)
        {
            var repository = GetRepository();
            var query = QueryFactory.Create<ProductSaleByDayEntity>();
            query.OrderBy(m => m.StatisticalDate);
            query.StarSize = 11;
            query.Rows = 1;
            query.DBModel.DBModel_ShuffledTempDate = dt;
            var model = repository.GetPaging(query).ToList()[0];
            var entity = repository.Get(new ProductSaleByDayEntity
            {
                DBModel_ShuffledTempDate = dt,//�����ʾ��19��1�µĿ�ͱ�
                SysNo = model.SysNo
            });
            Assert.AreEqual(model.StatisticalDate.Year,dt.Year);
            Assert.AreEqual(model.StatisticalDate.Month,dt.Month);
            Assert.NotNull(entity);
            Assert.AreEqual(model.SysNo, entity.SysNo);
            Assert.AreEqual(model.ShopName, entity.ShopName);
            Assert.AreEqual(model.StatisticalDate, entity.StatisticalDate);

            entity = repository.Get<ProductSaleByDayEntity>(model, $"select * from Purify_ProductSaleByDay_{dt.Month.ToString("00")} where SysNo=@SysNo", new { model.SysNo });
            Assert.NotNull(entity);
            Assert.AreEqual(model.SysNo, entity.SysNo);
            Assert.AreEqual(model.ShopName, entity.ShopName);
            entity = repository.Get(model, $"select * from Purify_ProductSaleByDay_{dt.Month.ToString("00")} where SysNo=@SysNo", new { model.SysNo });
            Assert.NotNull(entity);
            Assert.AreEqual(model.SysNo, entity.SysNo);
            Assert.AreEqual(model.ShopName, entity.ShopName);
        }

        [Test]
        public void TestProductSaleByDayGetList()
        {
            var repository = GetRepository();
            var query = QueryFactory.Create<ProductSaleByDayEntity>(m => m.ShopName.Contains("����"));

            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 02, 01);//�����ʾ��19��1�µĿ�ͱ�
            var q2 = query.Select(m => new ProductSaleByDayEntity { SysNo = m.SysNo, BrandID = m.BrandID, ShopName = m.ShopName, StatisticalDate = m.StatisticalDate });
            //�ֿ�Ĵ���stTime,endTime���Զ�����ʱ���ѯ���������Ŀ�ͱ�
            var lst = repository.GetList(q2);
            Assert.True(lst.Count > 30);
            Assert.False(lst.Any(m => !(m.ShopName?.Contains("����")).GetValueOrDefault() || m.StatisticalDate.Year != 2019 || m.StatisticalDate.Month != 2));
            query = QueryFactory.Create<ProductSaleByDayEntity>(m => m.IsExclude && !m.IsExclude && m.ProductName.Contains("���")
            && !m.ProductName.Contains("û��") && m.IsExclude == false && m.AveragePrice > 0 && m.AveragePrice == 0 && m.CategoryID == Guid.Empty && true == true);
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 02, 01);
            var c = repository.Count(query);
            Assert.True(c == 0);

            query = QueryFactory.Create<ProductSaleByDayEntity>();
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 02, 01);
            lst = repository.GetList(query);
            lst[0].ShopName = "";
            repository.Update(lst[0]);
            var all = lst.AsQueryable();

            Expression<Func<ProductSaleByDayEntity, bool>> where =
                m => m.ShopName.Contains("����") && !m.ShopName.Contains("������") && m.ShopName.Contains("����") == true
                && m.ShopName.Contains("������") == false;

            var allCount = all.Count(where);
            query = QueryFactory.Create(where);
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 02, 01);
            c = repository.Count(query);
            Assert.AreEqual(allCount, c);
            Assert.AreNotEqual(allCount, 0);


            where =
             m => m.Sales + m.NumberOfSales > m.OrderQuantity + m.NumberOfSales && m.Sales > 0;

            allCount = all.Count(where);
            query = QueryFactory.Create<ProductSaleByDayEntity>(where);
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 02, 01);
            c = repository.Count(query);
            Assert.AreEqual(allCount, c);
            Assert.AreNotEqual(allCount, 0);


            where =
             m => m.StatisticalDate > new DateTime(2019, 2, 10) && m.StatisticalDate <= new DateTime(2019, 2, 12);

            allCount = all.Count(where);
            query = QueryFactory.Create(where);
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 02, 01);
            c = repository.Count(query);
            Assert.AreEqual(allCount, c);
            Assert.AreNotEqual(allCount, 0);


            where =
           m => !string.IsNullOrEmpty(m.ShopName) && !string.IsNullOrEmpty(m.DataSource);
            allCount = all.Count(where);
            query = QueryFactory.Create<ProductSaleByDayEntity>(where);
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 02, 01);
            c = repository.Count(query);
            Assert.AreEqual(allCount, c);
            Assert.AreNotEqual(allCount, 0);

            where =
         m => string.IsNullOrEmpty(m.ShopName) && !string.IsNullOrEmpty(m.DataSource);
            allCount = all.Count(where);
            query = QueryFactory.Create(where);
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 02, 01);
            c = repository.Count(query);
            Assert.AreEqual(allCount, c);
            Assert.AreNotEqual(allCount, 0);


            where =
         m => !(string.IsNullOrEmpty(m.ShopName) && !string.IsNullOrEmpty(m.DataSource));
            allCount = all.Count(where);
            query = QueryFactory.Create(where);
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 02, 01);
            c = repository.Count(query);
            Assert.AreEqual(allCount, c);
            Assert.AreNotEqual(allCount, 0);
            var sysNos = new List<Guid>();

            where = m => sysNos.Contains(m.SysNo);
            allCount = all.Count(where);
            query = QueryFactory.Create(where);
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 02, 01);
            c = repository.Count(query);
            Assert.AreEqual(allCount, c);
            Assert.AreEqual(allCount, 0);

            sysNos = all.OrderBy(m => Guid.NewGuid()).Take(10).Select(m => m.SysNo).ToList();

            where = m => sysNos.Contains(m.SysNo);
            allCount = all.Count(where);
            query = QueryFactory.Create(where);
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 02, 01);
            c = repository.Count(query);
            Assert.AreEqual(allCount, c);
            Assert.AreEqual(allCount, 10);

            where = m => m.ProductID != Guid.Empty && sysNos.Contains(m.SysNo) && m.SysNo != Guid.NewGuid();
            allCount = all.Count(where);
            query = QueryFactory.Create(where);
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 02, 01);
            c = repository.Count(query);
            Assert.AreEqual(allCount, c);
            Assert.AreEqual(allCount, 10);

            var tempRes = query.Select(m => m.SysNo);
            where = m => tempRes.Contains(m.SysNo);
            query = QueryFactory.Create(where);
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 02, 01);
            c = repository.Count(query);
            Assert.AreEqual(c, 10);

            bool hasError = false;
            try
            {

                where = m => sysNos.Contains(m.SysNo);
                query = QueryFactory.Create(where);
                c = repository.Count(query);
            }
            catch (NoShuffledException)
            {
                hasError = true;
            }
            Assert.True(hasError);
        }
        [Test]
        public void TestProductSaleByDayGetPaging()
        {
            var stTime = new DateTime(2019, 1, 15);
            var endTime = new DateTime(2019, 2, 11);
            var repository = GetRepository();
            var query = QueryFactory.Create<ProductSaleByDayEntity>(m => m.ShopName.Contains("����"));
            query.And(m => m.StatisticalDate >= stTime);
            query.And(m => m.StatisticalDate < endTime.Date.AddDays(1));
            query.OrderByDescing(m => m.StatisticalDate);
            query.StarSize = 20;
            query.Rows = 10;
            //�ֿ�Ĵ���stTime,endTime���Զ�����ʱ���ѯ���������Ŀ�ͱ�
            var paging = repository.GetPaging(query, stTime, endTime);
            var count = paging.TotalCount;
            var lst = paging.ToList();//����paging.Items
            Assert.True(count > 10);
            Assert.True(lst.Count == 10);
            Assert.True(lst.Any(m => m.StatisticalDate > new DateTime(2019, 2, 1)));
        }




        private void TestProductSaleByDayAdd()
        {
            var addEntity = new ProductSaleByDayEntity()
            {
                SysNo = Guid.NewGuid(),
                DataSource = "������Դ",
                ProductID = Guid.NewGuid(),
                ShopID = Guid.NewGuid(),
                ShopName = "���Ե������",
                ProductName = "������Ʒ",
                OutProductID = Guid.NewGuid().ToString(),
                ImportGroupId = Guid.NewGuid(),
                StatisticalDate = DateTime.Now.AddYears(2019 - DateTime.Now.Year)//�ֿ�ֱ��ֶ��Ǳ����
            };
            var repository = GetRepository();
            //������������������л��Զ���ֵ������ֵ������
            repository.Add(addEntity);
            var entity = repository.Get(new ProductSaleByDayEntity { DBModel_ShuffledTempDate = addEntity.StatisticalDate, SysNo = addEntity.SysNo });
            Assert.NotNull(entity);
            Assert.AreEqual(addEntity.SysNo, entity.SysNo);
            Assert.AreEqual(addEntity.ShopName, entity.ShopName);
        }

        private void TestProductSaleByDayUpdate()
        {

            var repository = GetRepository();
            var queryCount = QueryFactory.Create<ProductSaleByDayEntity>();
            queryCount.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 01, 01);
            queryCount.And(m => m.DataSource == "������Դ�޸�");
            var preCount = repository.Count(queryCount);

            var query = QueryFactory.Create<ProductSaleByDayEntity>();
            query.And(m => m.DataSource != "������Դ�޸�");
            query.OrderByDescing(m => m.StatisticalDate);
            query.StarSize = new Random().Next(5);
            query.Rows = 1;
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 01, 01);
            var model = repository.GetPaging(query).ToList()[0];

            model.DataSource = "������Դ�޸�";
            model.ProductName = "������Ʒ�޸�";

            //�����������������ֶ�
            var r = repository.Update(model);
            Assert.True(r);
            var nextCount = repository.Count(queryCount);
            Assert.AreEqual(preCount + 1, nextCount);
            var entity = repository.Get(new ProductSaleByDayEntity { DBModel_ShuffledTempDate = model.StatisticalDate, SysNo = model.SysNo });
            Assert.NotNull(entity);
            Assert.AreEqual(model.SysNo, entity.SysNo);
            Assert.AreEqual(model.DataSource, entity.DataSource);
            Assert.AreEqual(model.ProductName, entity.ProductName);
        }
        [Test]
        public void TestProductSaleByDayAddUpdate()
        {
            TestProductSaleByDayAdd();
            TestProductSaleByDayUpdate();
            TestProductSaleByDayUpdateWhere();
            TestProductSaleByDayDelete();
        }
        public void TestProductSaleByDayDelete()
        {
            var repository = GetRepository();
            var query = QueryFactory.Create<ProductSaleByDayEntity>();
            query.And(m => !m.DataSource.Contains("�޸�"));
            query.OrderByDescing(m => m.StatisticalDate);
            query.StarSize = new Random().Next(5);
            query.Rows = 1;
            query.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 01, 05);
            var model = repository.GetPaging(query).ToList()[0];
            var qm = repository.Get(new ProductSaleByDayEntity
            {
                SysNo = model.SysNo,
                DBModel_ShuffledTempDate = new DateTime(2019, 01, 05)
            });
            Assert.NotNull(qm);
            var res = repository.Delete(model);
            Assert.True(res);
            qm = repository.Get(new ProductSaleByDayEntity
            {
                SysNo = model.SysNo,
                DBModel_ShuffledTempDate = new DateTime(2019, 01, 05)
            });
            Assert.Null(qm);
            model = repository.GetPaging(query).ToList()[0];
            qm = repository.Get(new ProductSaleByDayEntity
            {
                SysNo = model.SysNo,
                DBModel_ShuffledTempDate = new DateTime(2019, 01, 05)
            });
            Assert.NotNull(qm);
            res = repository.Delete(new ProductSaleByDayEntity
            {
                SysNo = model.SysNo,
                DBModel_ShuffledTempDate = new DateTime(2019, 01, 05)
            });
            Assert.True(res);
            qm = repository.Get(new ProductSaleByDayEntity
            {
                SysNo = model.SysNo,
                DBModel_ShuffledTempDate = new DateTime(2019, 01, 05)
            });
            Assert.Null(qm);
            var delQuery = QueryFactory.Create<ProductSaleByDayEntity>(m => m.DataSource == "������Դ�����޸�");
            delQuery.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 01, 05);
            var deleteCount = repository.Delete(delQuery);
            Assert.True(deleteCount > 0);

        }
        public void TestProductSaleByDayUpdateWhere()
        {

            var repository = GetRepository();
            var queryCount = QueryFactory.Create<ProductSaleByDayEntity>(m => !m.ProductName.Contains("û��") && m.ProductName.Contains("�޸�"));

            queryCount.DBModel.DBModel_ShuffledTempDate = new DateTime(2019, 01, 05);
            var count = repository.Count(queryCount);
            var updateEntity = new ProductSaleByDayEntity()
            {
                DataSource = "������Դ�����޸�",
                ShopName = "�����޸�Where",
                DBModel_ShuffledTempDate = new DateTime(2019, 01, 05),//�������仰��ȷ�����Ǹ��⼰��
                // StatisticalDate = statisticalDate,//���Ҫ����StatisticalDate���������仰��������Ǿ仰
            };

            var where = QueryFactory.Create<ProductSaleByDayEntity>(m => !m.ProductName.Contains("û��") && m.ProductName.Contains("�޸�"));//where�Ǹ�������
            //ע������Ǹ����õ���ʵ�����DBModel_ShuffledTempDate Query�е���Ч
            int updateCount = repository.Update(updateEntity, where);
            Assert.AreEqual(updateCount, count);
            Assert.AreNotEqual(updateCount, 0);

        }


        [Test]
        public void TestProductSaleByDayNSTransaction()
        {
            using (var tran = DBTool.BeginTransaction())
            {
                TestProductSaleByDayGet();
                TestProductSaleByDayGetList();
                TestProductSaleByDayGetPaging();
                TestProductSaleByDayAddUpdate();
                tran.Complete();
            }

        }
        [TearDown]
        public void TestTearDown()
        {
            DBTool.CloseConnections();
        }
    }
}