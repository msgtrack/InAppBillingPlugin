﻿using Plugin.InAppBilling.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using Windows.ApplicationModel.Store;

namespace Plugin.InAppBilling.UWPPrivate
{
	class InAppBillingImplOld : BaseInAppBilling
	{
		/// <summary>
		/// Default constructor
		/// </summary>
		public InAppBillingImplOld()
		{

			// Get current product object 
			// Execute only one of these statements. 
			// The next line is commented out for testing.
			//var currentApp = Windows.ApplicationModel.Store.CurrentApp;

			// The next line is commented out for production/release.
			//currentApp = Windows.ApplicationModel.Store.CurrentAppSimulator;

			// We should have either a real or a simulated CurrentProduct object here.

			// Get the license info
			var licenseInformation = Windows.ApplicationModel.Store.CurrentApp.LicenseInformation;

			var productInfo = licenseInformation.ProductLicenses["vlitw1_hd"];

			Debug.WriteLine("Product info:" + productInfo.IsActive);

			productInfo = licenseInformation.ProductLicenses["vsg1_sd"]; // This works!!!!
			Debug.WriteLine("Product info:" + productInfo.IsActive);

		}

		/// <summary>
		/// Gets or sets if in testing mode. Only for UWP
		/// </summary>
		public override bool InTestingMode { get; set; }

		/// <summary>
		/// Connect to billing service
		/// </summary>
		/// <returns>If Success</returns>
		public override Task<bool> ConnectAsync(ItemType itemType = ItemType.InAppPurchase) => Task.FromResult(true);

		/// <summary>
		/// Disconnect from the billing service
		/// </summary>
		/// <returns>Task to disconnect</returns>
		public override Task DisconnectAsync() => Task.CompletedTask;

		/// <summary>
		/// Gets product information
		/// </summary>
		/// <param name="itemType">Type of item</param>
		/// <param name="productIds">Product Ids</param>
		/// <returns></returns>
		public override async Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, params string[] productIds)
		{
			// Get list of products from store or simulator

			var listingInformation = await CurrentAppMock.LoadListingInformationByProductIdsAsync(InTestingMode, productIds);

			var products = new List<InAppBillingProduct>();
			foreach (var productId in productIds)
			{
				// Check if requested product exists
				if (!listingInformation.ProductListings.ContainsKey(productId))
					continue;

				// Get product and transform it to an InAppBillingProduct
				var product = listingInformation.ProductListings[productId];
				products.Add(new InAppBillingProduct
				{
					Name = product.Name,
					Description = product.Description,
					ProductId = product.ProductId,
					LocalizedPrice = product.FormattedPrice,
					//CurrencyCode = product.CurrencyCode // Does not work at the moment, as UWP throws an InvalidCastException when getting CurrencyCode
				});
			}

			return products;
		}

		protected override async Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, IInAppBillingVerifyPurchase verifyPurchase, string verifyOnlyProductId)
		{
			await CurrentAppMock.GetProductReceiptAsync(InTestingMode, "vsg1_sd");
			// Get list of product receipts from store or simulator
			var xmlReceipt = await CurrentAppMock.GetAppReceiptAsync(InTestingMode);

			// Transform it to list of InAppBillingPurchase
			return xmlReceipt.ToInAppBillingPurchase(ProductPurchaseStatus.AlreadyPurchased);
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
		public override async Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase = null)
		{
			//var licinfo = CurrentApp.LicenseInformation;
			// Get purchase result from store or simulator
			var purchaseResult = await CurrentAppMock.RequestProductPurchaseAsync(InTestingMode, productId);

			var recStr = await CurrentAppMock.GetProductReceiptAsync(InTestingMode, productId);



			//await GetPurchasesAsync(ItemType.InAppPurchase, null, null);
			if (purchaseResult == null)
			{
				return null;
			}
			if (string.IsNullOrWhiteSpace(purchaseResult.ReceiptXml))
				return null;

			// Transform it to InAppBillingPurchase
			InAppBillingPurchase purchase = purchaseResult.ReceiptXml.ToInAppBillingPurchase(purchaseResult.Status).FirstOrDefault();

			if (verifyPurchase == null)
				return purchase;

			var validated = await verifyPurchase.VerifyPurchase(purchaseResult.ReceiptXml, string.Empty, purchase.ProductId, purchase.Id);

			return validated ? purchase : null;
		}

		/// <summary>
		/// Consume a purchase with a purchase token.
		/// </summary>
		/// <param name="productId">Id or Sku of product</param>
		/// <param name="purchaseToken">Original Purchase Token</param>
		/// <returns>If consumed successful</returns>
		/// <exception cref="InAppBillingPurchaseException">If an error occures during processing</exception>
		public override async Task<InAppBillingPurchase> ConsumePurchaseAsync(string productId, string purchaseToken)
		{
			var result = await CurrentAppMock.ReportConsumableFulfillmentAsync(InTestingMode, productId, new Guid(purchaseToken));
			switch (result)
			{
				case FulfillmentResult.ServerError:
					throw new InAppBillingPurchaseException(PurchaseError.AppStoreUnavailable);
				case FulfillmentResult.NothingToFulfill:
					throw new InAppBillingPurchaseException(PurchaseError.ItemUnavailable);
				case FulfillmentResult.PurchasePending:
				case FulfillmentResult.PurchaseReverted:
					throw new InAppBillingPurchaseException(PurchaseError.GeneralError);
				case FulfillmentResult.Succeeded:
					return new InAppBillingPurchase
					{
						Id = purchaseToken,
						AutoRenewing = false,
						Payload = string.Empty,
						PurchaseToken = purchaseToken,
						ProductId = productId,
						State = PurchaseState.Purchased,
						TransactionDateUtc = DateTime.UtcNow
					};
				default:
					return null;
			}
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
		public override async Task<InAppBillingPurchase> ConsumePurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase = null)
		{
			var items = await CurrentAppMock.GetAvailableConsumables(InTestingMode);

			var consumable = items.FirstOrDefault(i => i.ProductId == productId);
			if (consumable == null)
				throw new InAppBillingPurchaseException(PurchaseError.ItemUnavailable);

			var result = await CurrentAppMock.ReportConsumableFulfillmentAsync(InTestingMode, productId, consumable.TransactionId);
			switch (result)
			{
				case FulfillmentResult.ServerError:
					throw new InAppBillingPurchaseException(PurchaseError.GeneralError);
				case FulfillmentResult.NothingToFulfill:
					throw new InAppBillingPurchaseException(PurchaseError.ItemUnavailable);
				case FulfillmentResult.PurchasePending:
				case FulfillmentResult.PurchaseReverted:
					throw new InAppBillingPurchaseException(PurchaseError.GeneralError);
				case FulfillmentResult.Succeeded:
					return new InAppBillingPurchase
					{
						AutoRenewing = false,
						Id = consumable.TransactionId.ToString(),
						Payload = payload,
						ProductId = consumable.ProductId,
						PurchaseToken = consumable.TransactionId.ToString(),
						State = PurchaseState.Purchased,
						TransactionDateUtc = DateTime.UtcNow
					};
				default:
					return null;
			}
		}
	}

	/// <summary>
	/// Unfortunately, CurrentApp and CurrentAppSimulator do not share an interface or base class
	/// This is why, we use a mocking class here
	/// </summary>
	static class CurrentAppMock
	{
		public static async Task<IEnumerable<UnfulfilledConsumable>> GetAvailableConsumables(bool isTestingMode)
		{
			return isTestingMode ? await CurrentAppSimulator.GetUnfulfilledConsumablesAsync() : await CurrentApp.GetUnfulfilledConsumablesAsync();
		}

		public static async Task<FulfillmentResult> ReportConsumableFulfillmentAsync(bool isTestingMode, string productId, Guid transactionId)
		{
			return isTestingMode ? await CurrentAppSimulator.ReportConsumableFulfillmentAsync(productId, transactionId) : await CurrentApp.ReportConsumableFulfillmentAsync(productId, transactionId);
		}

		public static async Task<string> GetProductReceiptAsync(bool isTestingMode, String productId)
		{
			return isTestingMode ? await CurrentAppSimulator.GetProductReceiptAsync(productId) : await CurrentApp.GetProductReceiptAsync(productId);
		}
		//public static async Task<ListingInformation> LoadListingInformationAsync(bool isTestingMode)
		//{
		//   return isTestingMode ? await CurrentAppSimulator.LoadListingInformationAsync() : await CurrentApp.LoadListingInformationAsync();
		//}

		public static async Task<ListingInformation> LoadListingInformationByProductIdsAsync(bool isTestingMode, IEnumerable<string> productIds)
		{
			// This won't work until the app is public
			return isTestingMode ? await CurrentAppSimulator.LoadListingInformationByProductIdsAsync(productIds) : await CurrentApp.LoadListingInformationByProductIdsAsync(productIds);
		}

		public static async Task<string> GetAppReceiptAsync(bool isTestingMode)
		{
			return isTestingMode ? await CurrentAppSimulator.GetAppReceiptAsync() : await CurrentApp.GetAppReceiptAsync();
		}

		public static async Task<PurchaseResults> RequestProductPurchaseAsync(bool isTestingMode, string productId)
		{
			return isTestingMode ? await CurrentAppSimulator.RequestProductPurchaseAsync(productId) : await CurrentApp.RequestProductPurchaseAsync(productId);
		}
	}

	static class InAppBillingHelperUwp
	{
		/// <summary>
		/// Read purchase data out of the UWP Receipt XML
		/// </summary>
		/// <param name="xml">Receipt XML</param>
		/// <param name="status">Status of the purchase</param>
		/// <returns>A list of purchases, the user has done</returns>
		public static IEnumerable<InAppBillingPurchase> ToInAppBillingPurchase(this string xml, ProductPurchaseStatus status)
		{
			var purchases = new List<InAppBillingPurchase>();

			var xmlDoc = new XmlDocument();
			try
			{
				xmlDoc.LoadXml(xml);
			}
			catch
			{
				//Invalid XML, we haven't finished this transaction yet.
			}

			// Iterate through all ProductReceipt elements
			var xmlProductReceipts = xmlDoc.GetElementsByTagName("ProductReceipt");
			for (var i = 0; i < xmlProductReceipts.Count; i++)
			{
				var xmlProductReceipt = xmlProductReceipts[i];

				// Create new InAppBillingPurchase with values from the xml element
				var purchase = new InAppBillingPurchase()
				{
					Id = xmlProductReceipt.Attributes["Id"].Value,
					TransactionDateUtc = Convert.ToDateTime(xmlProductReceipt.Attributes["PurchaseDate"].Value),
					ProductId = xmlProductReceipt.Attributes["ProductId"].Value,
					AutoRenewing = false // Not supported by UWP yet
				};
				purchase.PurchaseToken = purchase.Id;
				// Map native UWP status to PurchaseState
				switch (status)
				{
					case ProductPurchaseStatus.AlreadyPurchased:
					case ProductPurchaseStatus.Succeeded:
						purchase.State = PurchaseState.Purchased;
						break;
					case ProductPurchaseStatus.NotFulfilled:
						purchase.State = PurchaseState.Deferred;
						break;
					case ProductPurchaseStatus.NotPurchased:
						purchase.State = PurchaseState.Canceled;
						break;
					default:
						purchase.State = PurchaseState.Unknown;
						break;
				}

				// Add to list of purchases
				purchases.Add(purchase);
			}

			return purchases;
		}
	}
}