﻿using Dakata.Examples.Dal;
using FluentAssertions;
using System.Threading.Tasks;
using Xunit;

namespace Dakata.Examples
{
    public partial class Examples
    {
        [Fact]
        public async Task SimpleInclude()
        {
            var purchaseOrderDal = new PurchaseOrderDal(
                CreateDapperConnection(),
                sqlInfo => _testOutputHelper.WriteLine($"The sql is {sqlInfo.Sql}")
            );
            
            var purchaseOrder = await purchaseOrderDal.GetPurchaseOrderWithLinesAndPackageType2(5);
            purchaseOrder.PurchaseOrderLines.Should().NotBeNull();
            purchaseOrder.PurchaseOrderLines.ForEach(x => x.PackageType.Should().NotBeNull());

            purchaseOrder = await purchaseOrderDal.GetPurchaseOrderWithLinesAndPackageType3(5);
            purchaseOrder.PurchaseOrderLines.Should().NotBeNull();
            purchaseOrder.PurchaseOrderLines.ForEach(x => x.PackageType.Should().NotBeNull());
        }

        [Fact]
        public async Task DeepInclude()
        {
            var purchaseOrderDal = new PurchaseOrderDal(
                CreateDapperConnection(),
                sqlInfo => _testOutputHelper.WriteLine($"The sql is {sqlInfo.Sql}")
            );

            var purchaseOrder = await purchaseOrderDal.GetPurchaseOrderWithLinesAndPackageType4(5);
            purchaseOrder.PurchaseOrderLines.Should().NotBeNull();
            purchaseOrder.PurchaseOrderLines.ForEach(x => x.PackageType.Should().NotBeNull());
        }
    }
}