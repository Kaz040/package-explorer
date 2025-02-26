/********************************************************************************
* Copyright (c) {2019 - 2024} Contributors to the Eclipse Foundation
*
* See the NOTICE file(s) distributed with this work for additional
* information regarding copyright ownership.
*
* This program and the accompanying materials are made available under the
* terms of the Apache License Version 2.0 which is available at
* https://www.apache.org/licenses/LICENSE-2.0
*
* SPDX-License-Identifier: Apache-2.0
********************************************************************************/

/*
Copyright (c) 2018-2023 Festo SE & Co. KG <https://www.festo.com/net/de_de/Forms/web/contact_international>
Author: Michael Hoffmeister

This source code is licensed under the Apache License 2.0 (see LICENSE.txt).

This source code may use other Open Source software components (see LICENSE.txt).
*/

using Aas = AasCore.Aas3_0;

namespace AasxPredefinedConcepts
{
    /// <summary>
    /// Definitions for operation of Package Explorer
    /// </summary>
    public class PackageExplorer : AasxDefinitionBase
    {
        public static PackageExplorer Static = new PackageExplorer();

        public Aas.ConceptDescription
            CD_AasxLoadedNavigateTo;

        public PackageExplorer()
        {
            // info
            this.DomainInfo = "AASX PackageExplorer - General";

            // IReferable
            CD_AasxLoadedNavigateTo = CreateSparseConceptDescription("en", "IRI",
                "AasxLoadedNavigateTo",
                "http://admin-shell.io/aasx-package-explorer/main/AasxLoadedNavigateTo/1/0",
                "Specifies a ReferenceElement, to which the Package Explorer will refer directly " +
                "after loading. Can be situated in any Submodel, but on first hierarchy level.");

            // reflect
            AddEntriesByReflection(this.GetType(), useAttributes: false, useFieldNames: true);
        }
    }
}
