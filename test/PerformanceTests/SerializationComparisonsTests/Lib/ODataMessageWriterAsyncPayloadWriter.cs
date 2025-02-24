﻿//---------------------------------------------------------------------
// <copyright file="ODataMessageWriterAsyncPayloadWriter.cs" company="Microsoft">
//      Copyright (C) Microsoft Corporation. All rights reserved. See License.txt in the project root for license information.
// </copyright>
//---------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.OData;
using Microsoft.OData.Edm;

namespace ExperimentsLib
{
    /// <summary>
    /// Writes Customer collection OData JSON format using <see cref="ODataMessageWriter"/> async version.
    /// </summary>
    public class ODataMessageWriterAsyncPayloadWriter : IPayloadWriter<IEnumerable<Customer>>
    {
        private readonly IEdmModel model;
        private readonly bool enableValidation;
        private readonly Func<Stream, IODataResponseMessage> messageFactory;

        public ODataMessageWriterAsyncPayloadWriter(IEdmModel model, Func<Stream, IODataResponseMessage> messageFactory, bool enableValidation = true)
        {
            this.model = model;
            this.enableValidation = enableValidation;
            this.messageFactory = messageFactory;
        }

        /// <inheritdoc/>
        public async Task WritePayloadAsync(IEnumerable<Customer> payload, Stream stream)
        {
            ODataMessageWriterSettings settings = new ODataMessageWriterSettings();
            settings.ODataUri = new ODataUri
            {
                ServiceRoot = new Uri("https://services.odata.org/V4/OData/OData.svc/")
            };

            if (!this.enableValidation)
            {
                settings.Validations = ValidationKinds.None;
                settings.EnableCharactersCheck = false;
                settings.AlwaysAddTypeAnnotationsForDerivedTypes = false;
            }
            
            IODataResponseMessage message = this.messageFactory(stream);

            var messageWriter = new ODataMessageWriter(message, settings, this.model);
            var entitySet = this.model.EntityContainer.FindEntitySet("Customers");
            ODataWriter writer = await messageWriter.CreateODataResourceSetWriterAsync(entitySet);

            var resourceSet = new ODataResourceSet();
            await writer.WriteStartAsync(resourceSet);

            foreach (var customer in payload)
            {
                // await resourceSerializer.WriteObjectInlineAsync(item, elementType, writer, writeContext);
                // create resource with only primitive types
                var resource = new ODataResource
                {
                    Properties = new[]
                    {
                        new ODataProperty
                        {
                            Name = "Id",
                            Value = customer.Id
                        },
                        new ODataProperty { Name = "Name", Value = customer.Name },
                        new ODataProperty
                        {
                            Name = "Emails",
                            Value = new ODataCollectionValue
                            {
                                Items = customer.Emails,
                                TypeName = "Collection(Edm.String)"
                            }
                        }
                    }
                };

                await writer.WriteStartAsync(resource);
                // skip WriterStreamPropertiesAsync
                // WriteComplexPropertiesAsync
                // -- HomeAddress
                var homeAddressInfo = new ODataNestedResourceInfo
                {
                    Name = "HomeAddress",
                    IsCollection = false
                };
                // start write homeAddress
                await writer.WriteStartAsync(homeAddressInfo);

                var homeAddressResource = new ODataResource
                {
                    Properties = new[]
                    {
                        new ODataProperty { Name = "City", Value = customer.HomeAddress.City },
                        new ODataProperty { Name = "Street", Value = customer.HomeAddress.Street }
                    }
                };

                await writer.WriteStartAsync(homeAddressResource);
                await writer.WriteEndAsync();

                // end write homeAddress
                await writer.WriteEndAsync();
                // -- End HomeAddress

                // -- Addresses
                var addressesInfo = new ODataNestedResourceInfo
                {
                    Name = "Addresses",
                    IsCollection = true
                };
                // start addressesInfo
                await writer.WriteStartAsync(addressesInfo);

                var addressesResourceSet = new ODataResourceSet();
                // start addressesResourceSet
                await writer.WriteStartAsync(addressesResourceSet);

                foreach (var address in customer.Addresses)
                {
                    var addressResource = new ODataResource
                    {
                        Properties = new[]
                        {
                            new ODataProperty { Name = "City", Value = address.City },
                            new ODataProperty { Name = "Street", Value = address.Street }
                        }
                    };

                    await writer.WriteStartAsync(addressResource);
                    await writer.WriteEndAsync();
                }

                // end addressesResourceSet
                await writer.WriteEndAsync();


                // end addressesInfo
                await writer.WriteEndAsync();

                // -- End Addresses

                // end write resource
                await writer.WriteEndAsync();
            }

            await writer.WriteEndAsync();
            await writer.FlushAsync();
        }
    }
}
