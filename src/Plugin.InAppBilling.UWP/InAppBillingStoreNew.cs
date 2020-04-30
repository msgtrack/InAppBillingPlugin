using Plugin.InAppBilling.Abstractions;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Services.Store;

namespace Plugin.InAppBilling
{
	class InAppBillingStoreNew : BaseInAppBilling
	{

		private StoreContext storeContext = StoreContext.GetDefault();
		private IEnumerable<StoreProduct> lastListOfProducts = null;

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

		public override Task<InAppBillingPurchase> ConsumePurchaseAsync(string productId, string purchaseToken)
		{
			throw new NotImplementedException();
		}

		public override Task<InAppBillingPurchase> ConsumePurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase = null)
		{
			throw new NotImplementedException();
		}

		private async Task<string> GetMSProductIDForPID( string pid )
		{
			if( lastListOfProducts == null )
			{
				await GetProductInfoAsync(ItemType.InAppPurchase, new string[]{ "" } );
			}
			// TODO: determine MS product id for the PID
			foreach( var product in lastListOfProducts )
			{
				if( product.InAppOfferToken.CompareTo(pid) == 0 )
				{
					return product.StoreId;
				}
			}
			return null;
		}

		public async override Task<IEnumerable<InAppBillingProduct>> GetProductInfoAsync(ItemType itemType, params string[] productIds)
		{
			// Create a filtered list of the product AddOns I care about
			string[] filterList = new string[] { "Consumable", "Durable", "UnmanagedConsumable" };

			// Get list of Add Ons this app can sell, filtering for the types we know about
			StoreProductQueryResult addOns = await storeContext.GetAssociatedStoreProductsAsync(filterList);

			if (addOns.ExtendedError != null)
			{
				Debug.WriteLine("GetProductInfoAsync Extended Error:" + addOns.ExtendedError.ToString());
				throw new InAppBillingPurchaseException(PurchaseError.GeneralError, addOns.ExtendedError);
			}

			StoreProduct product;

			var products = new List<InAppBillingProduct>();

			foreach (KeyValuePair<string, StoreProduct> kvp in addOns.Products)
			{
				Debug.WriteLine("Store Product: key" + kvp.Key + " Product:" + kvp.Value.InAppOfferToken + " " + kvp.Value.StoreId + " " + kvp.Value.ToString());


				foreach (string pid in productIds)
				{
					if(pid.CompareTo(kvp.Value.InAppOfferToken) == 0 )
					{
						product = kvp.Value;
						products.Add(new InAppBillingProduct
						{
							Name = product.Title,
							Description = product.Description,
							ProductId = kvp.Value.InAppOfferToken,
							LocalizedPrice = product.Price.FormattedPrice,
							CurrencyCode = product.Price.CurrencyCode
							//CurrencyCode = product.CurrencyCode // Does not work at the moment, as UWP throws an InvalidCastException when getting CurrencyCode
						});
					}
					
				}
			}

			lastListOfProducts = addOns.Products.Values;

			return products;
		}

		public async override Task<InAppBillingPurchase> PurchaseAsync(string productId, ItemType itemType, string payload, IInAppBillingVerifyPurchase verifyPurchase = null)
		{
			string mspid = await GetMSProductIDForPID(productId);
			if( mspid == null )
			{
				throw new InAppBillingPurchaseException(PurchaseError.InvalidProduct);
			}


			StorePurchaseResult result = await storeContext.RequestPurchaseAsync(mspid);
			if (result.ExtendedError != null)
			{
				Debug.WriteLine("Extended Error:" + result.ExtendedError.ToString() + " Result: " + result.Status.ToString());
				throw new InAppBillingPurchaseException(PurchaseError.GeneralError, result.ExtendedError );
			}
			else
			{
				switch (result.Status)
				{
					case StorePurchaseStatus.AlreadyPurchased:
						throw new InAppBillingPurchaseException(PurchaseError.AlreadyOwned);
					case StorePurchaseStatus.Succeeded:
						var purchase = new InAppBillingPurchase()
						{
							Id = productId,
							ProductId = productId,
							AutoRenewing = false, // Not supported by UWP yet
							State = PurchaseState.Purchased,
							PurchaseToken = productId
						};
						return purchase;

					case StorePurchaseStatus.NotPurchased:
						throw new InAppBillingPurchaseException(PurchaseError.UserCancelled);

					case StorePurchaseStatus.NetworkError:
						throw new InAppBillingPurchaseException(PurchaseError.ServiceUnavailable);

					case StorePurchaseStatus.ServerError:
						throw new InAppBillingPurchaseException(PurchaseError.GeneralError);

					default:
						Debug.WriteLine("Product was not purchased due to an unknown error.");
						throw new InAppBillingPurchaseException(PurchaseError.GeneralError);
				}
			}
			return null;
		}

		protected async override Task<IEnumerable<InAppBillingPurchase>> GetPurchasesAsync(ItemType itemType, IInAppBillingVerifyPurchase verifyPurchase, string verifyOnlyProductId)
		{
			StoreAppLicense license = await storeContext.GetAppLicenseAsync();

			var products = new List<InAppBillingPurchase>();

			foreach (KeyValuePair<string, StoreLicense> kvp in license.AddOnLicenses)
			{
				Debug.WriteLine("Store license for:" + kvp.Key + " Lic:" + kvp.Value.InAppOfferToken + " " + kvp.Value.SkuStoreId + " " + kvp.Value.ExtendedJsonData);
				products.Add(new InAppBillingPurchase
				{
					Id = kvp.Key,
					ProductId = kvp.Value.InAppOfferToken,
					State = PurchaseState.Purchased,
		//			AutoRenewing = false,
	//				PurchaseToken
//					ConsumptionState = ConsumptionState.NoYetConsumed
				});
			}

			//return products;
			return null;
		}

		
	}
}
