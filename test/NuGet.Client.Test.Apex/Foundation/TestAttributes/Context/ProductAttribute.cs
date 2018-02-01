using System;

namespace NuGetClient.Test.Foundation.TestAttributes.Context
{
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
    public class ProductAttribute : ContextTraitAttribute
    {
        public ProductAttribute(Product product)
            : base("Product", product.ToString())
        {
            this.Product = product;
        }

        public Product Product { get; private set; }
        public override Context CreateContext(Context defaultContext)
        {
            return new Context(
                defaultContext,
                product: this.Product);
        }

        public override bool Equals(object obj)
        {
            ProductAttribute rhs = obj as ProductAttribute;
            if (rhs == null)
            {
                return false;
            }

            return this.Product == rhs.Product;
        }

        public override string GenerateStringValue()
        {
            return Product.ToString();
        }

        public override int GetHashCode()
        {
            return (this.Product.ToString().GetHashCode());
        }
    }
}
