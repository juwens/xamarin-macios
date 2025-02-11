using CoreFoundation;
using ObjCRuntime;
using Foundation;

using System;

namespace ThreadNetwork {

	[iOS (15,0)]
	[BaseType (typeof (NSObject))]
	interface THClient
	{
		[Async]
		[Export ("retrieveAllCredentials:")]
		void RetrieveAllCredentials (Action<NSSet<THCredentials>, NSError> completion);

		[Async]
		[Export ("deleteCredentialsForBorderAgent:completion:")]
		void DeleteCredentialsForBorderAgent (NSData borderAgentId, Action<NSError> completion);

		[Async]
		[Export ("retrieveCredentialsForBorderAgent:completion:")]
		void RetrieveCredentialsForBorderAgent (NSData borderAgentId, Action<THCredentials, NSError> completion);

		[Async]
		[Export ("storeCredentialsForBorderAgent:activeOperationalDataSet:completion:")]
		void StoreCredentialsForBorderAgent (NSData borderAgentId, NSData activeOperationalDataSet, Action<NSError> completion);

		[Async]
		[Export ("retrievePreferredCredentials:")]
		void RetrievePreferredCredentials (Action<THCredentials, NSError> completion);

		[Async]
		[Export ("retrieveCredentialsForExtendedPANID:completion:")]
		void RetrieveCredentialsForExtendedPanId (NSData extendedPanId, Action<THCredentials, NSError> completion);
	}

	[iOS (15,0)]
	[BaseType (typeof (NSObject))]
	[DisableDefaultCtor]
	interface THCredentials : NSSecureCoding
	{
		[NullAllowed, Export ("networkName")]
		string NetworkName { get; }

		[NullAllowed, Export ("extendedPANID")]
		NSData ExtendedPanId { get; }

		[NullAllowed, Export ("borderAgentID")]
		NSData BorderAgentId { get; }

		[NullAllowed, Export ("activeOperationalDataSet")]
		NSData ActiveOperationalDataSet { get; }

		[NullAllowed, Export ("networkKey")]
		NSData NetworkKey { get; }

		[NullAllowed, Export ("PSKC")]
		NSData Pskc { get; }

		[Export ("channel")]
		byte Channel { get; set; }

		[NullAllowed, Export ("panID")]
		NSData PanId { get; }

		[NullAllowed, Export ("creationDate")]
		NSDate CreationDate { get; }

		[NullAllowed, Export ("lastModificationDate")]
		NSDate LastModificationDate { get; }
	}

}
