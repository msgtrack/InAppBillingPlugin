using Plugin.InAppBilling.Abstractions;
using Plugin.InAppBilling.UWPPrivate;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Xml;
using Windows.ApplicationModel.Store;

namespace Plugin.InAppBilling
{
    /// <summary>
    /// Implementation for Feature
    /// </summary>
    public class InAppBillingImplementation : BaseInAppBilling
    {
		private BaseInAppBilling currentInAppBilling;
		private const Boolean useOldOne = false;


		/// <summary>
		/// Default constructor
		/// </summary>
		public InAppBillingImplementation()
        {

			if( useOldOne )
			{
				currentInAppBilling = new InAppBillingImplOld();
			}
			else
			{
				currentInAppBilling = new InAppBillingStoreNew();
			}

		}
		/// <summary>
		/// Gets or sets if in testing mode. Only for UWP
		/// </summary>
		public override bool InTestingMode { get { return currentInAppBilling.InTestingMode;  } set { currentInAppBilling.InTestingMode = value; } }


		/// <summary>
		/// Connect to billing service
		/// </summary>
		/// <returns>If Success</returns>
		public override Task<bool> ConnectAsync(ItemType itemType = ItemType.InAppPurchase)
		{
			return currentInAppBilling.ConnectAsync(itemType);
		}

        /// <summary>
        /// Disconnect from the billing service
        /// </summary>
        /// <returns>Task to disconnect</returns>
        public override Task DisconnectAsync()
		{
			return currentInAppBilling.DisconnectAsync();
		}

        /// <summary>
        /// Gets product information
        /// </summary>
        /// <param name="itemType">Type of item</param>
        /// <param name="productIds">Product Ids</param>
        /// <returns></returns>
        public async override Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, params string[] productIds)
        {
            return await currentInAppBilling.GetProductInfoAsync(itemType, productIds);
        }

        protected async override Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, IInAppBillingVerifyPurchase verifyPurchase, string verifyOnlyProductId)
        {
            return await currentInAppBilling.GetPurchasesAsync(itemType,verifyPurchase);
        }

        /// <summary>
        /// Purchase a specific product or subscription
        /// </summary>
        /// <param name="productId">Sku or ID of product</param>
        /// <param name="itemType">Type of product being requested</param>
        /// <param name="payload">Developer specific payload</param>
        /// <param name="verifyPurchase">Verify purchase implementation</param>
        /// <returns></returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occurs during processing</exception>
        public async override Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase = null)
        {
			return await currentInAppBilling.PurchaseAsync(productId, itemType, payload, verifyPurchase);
		}

		/// <summary>
		/// Consume a purchase with a purchase token.
		/// </summary>
		/// <param name="productId">Id or Sku of product</param>
		/// <param name="purchaseToken">Original Purchase Token</param>
		/// <returns>If consumed successful</returns>
		/// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
		public async override Task<InAppBillingPurchase> ConsumePurchaseAsync(string productId, string purchaseToken)
        {
			return await currentInAppBilling.ConsumePurchaseAsync(productId, purchaseToken);
        }

        /// <summary>
        /// Consume a purchase
        /// </summary>
        /// <param name="productId">Id/Sku of the product</param>
        /// <param name="payload">Developer specific payload of original purchase</param>
        /// <param name="itemType">Type of product being consumed.</param>
        /// <param name="verifyPurchase">Verify Purchase implementation</param>
        /// <returns>If consumed successful</returns>
        /// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
        public async override Task<InAppBillingPurchase> ConsumePurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase = null)
        {
			return await currentInAppBilling.ConsumePurchaseAsync(productId, itemType, payload, verifyPurchase);
        }
    }

	
    
}
