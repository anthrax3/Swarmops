﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Web;
using System.Web.Services;
using System.Web.UI;
using System.Web.UI.WebControls;
using Swarmops.Logic.Financial;
using Swarmops.Logic.Security;

namespace Swarmops.Frontend.Pages.v5.Ledgers
{
    public partial class BalanceTransactions : PageV5Base
    {
        protected void Page_Load (object sender, EventArgs e)
        {
            PageAccessRequired = new Access (CurrentOrganization, AccessAspect.BookkeepingDetails, AccessType.Read);

            RegisterControl (EasyUIControl.DataGrid);
            Localize();

        }

        private void Localize()
        {
            this.PageTitle = Resources.Pages.Ledgers.BalanceTransactions_PageTitle;
            this.InfoBoxLiteral = Resources.Pages.Ledgers.BalanceTransactions_Info;
            this.LabelHeaderUnbalancedTransactions.Text =
                Resources.Pages.Ledgers.BalanceTransactions_HeaderUnbalancedTransactions;

            this.LabelGridHeaderAccountName.Text = Resources.Pages.Ledgers.BalanceTransactions_GridHeader_AccountName;
            this.LabelGridHeaderAction.Text = Resources.Global.Global_Action;
            this.LabelGridHeaderDateTime.Text = Resources.Global.Global_Timestamp;
            this.LabelGridHeaderDelta.Text = Resources.Pages.Ledgers.BalanceTransactions_GridHeader_Amount;
            this.LabelGridHeaderId.Text = Resources.Pages.Ledgers.BalanceTransactions_GridHeader_TransactionId;
            this.LabelGridHeaderDescription.Text = Resources.Pages.Ledgers.BalanceTransactions_GridHeader_Description;

            this.LiteralModalHeader.Text = Resources.Pages.Ledgers.BalanceTransactions_ModalHeader;
            this.LabelDoYouWishTo.Text = Resources.Pages.Ledgers.BalanceTransactions_ModalDialog_DoYouWishTo;

            this.LabelDescribeBalance.Text = Resources.Pages.Ledgers.BalanceTransactions_ModalDialog_DescribeBalance;
            this.LabelRadioBalance.Text = Resources.Pages.Ledgers.BalanceTransactions_ModalDialog_RadioBalance;

            this.LabelDescribePayout.Text = Resources.Pages.Ledgers.BalanceTransactions_ModalDialog_DescribePayout;
            this.LabelDescribePayoutForeign.Text = Resources.Pages.Ledgers.BalanceTransactions_ModalDialog_DescribePayoutForeign;
            this.LabelDescribeOutboundInvoice.Text = 
            this.LabelRadioPayout.Text = Resources.Pages.Ledgers.BalanceTransactions_ModalDialog_RadioPayout;
            this.LabelRadioPayoutForeign.Text =
                String.Format(Resources.Pages.Ledgers.BalanceTransactions_ModalDialog_RadioPayoutForeign, 5);
            this.LabelRadioOutboundInvoice.Text =

            this.LiteralButtonBalance.Text = JavascriptEscape(Resources.Pages.Ledgers.BalanceTransactions_ModalDialog_ButtonBalance);
            this.LiteralButtonPayout.Text = JavascriptEscape(Resources.Pages.Ledgers.BalanceTransactions_ModalDialog_ButtonPayout);
            this.LiteralButtonPayoutForeign.Text = JavascriptEscape(Resources.Pages.Ledgers.BalanceTransactions_ModalDialog_ButtonPayout);
            this.LiteralButtonOutboundInvoice.Text = JavascriptEscape(Resources.Pages.Ledgers.BalanceTransactions_ModalDialog_ButtonOutboundInvoice);
        }


        [Serializable]
        public class TransactionMatchabilityData
        {
            public string TransactionDate { get; set; }
            public string DifferingAmount { get; set; }
            public DropdownOptions OpenPayoutData { get; set; }
            public DropdownOptions OpenOutboundInvoiceData { get; set; }
            public DropdownOptions OpenVatReports { get; set; }
        }

        [Serializable]
        public class DropdownOptions
        {
            public DropdownOption[] ExactMatches { get; set; }
            public DropdownOption[] TolerantMatches { get; set; }
        }

        [Serializable]
        public class DropdownOption
        {
            // Fields are lowercase for immediate usability in JavaScript without conversion.
            // ReSharper disable InconsistentNaming
            public string id;
            public string text;
            public string group;
            // ReSharper restore InconsistentNaming
        }


        [WebMethod]
        public static TransactionMatchabilityData GetTransactionMatchability (int transactionId)
        {
            AuthenticationData authData = GetAuthenticationDataAndCulture();

            if (
                !authData.Authority.HasAccess (new Access (authData.CurrentOrganization,
                    AccessAspect.BookkeepingDetails)))
            {
                throw new UnauthorizedAccessException();
            }

            FinancialTransaction transaction = FinancialTransaction.FromIdentity (transactionId);
            if (transaction.OrganizationId != authData.CurrentOrganization.Identity)
            {
                throw new UnauthorizedAccessException();
            }

            TransactionMatchabilityData result = new TransactionMatchabilityData();

            result.TransactionDate = transaction.DateTime.ToString ("yyyy-MMM-dd HH:mm");
            result.DifferingAmount = String.Format ("{0} {1:+#,#.00;−#,#.00;0}",
                // this is a UNICODE MINUS (U+2212), not the hyphen on the keyboard
                authData.CurrentOrganization.Currency.DisplayCode, transaction.Rows.AmountCentsTotal/100.0);

            if (transaction.Rows.AmountCentsTotal > 0)
            {
                result.OpenOutboundInvoiceData = GetOpenOutboundInvoiceData(transaction);
                result.OpenVatReports = null; // TODO
            }
            else
            {
                // Negative difference

                result.OpenPayoutData = GetOpenPayoutData(transaction);
            }

            return result;
        }

        private static DropdownOptions GetOpenPayoutData(FinancialTransaction transaction)
        {
            DateTime transactionDateTime = transaction.DateTime;
            Int64 matchAmount = transaction.Rows.AmountCentsTotal;

            DropdownOptions result = new DropdownOptions();

            List<DropdownOption> listExact = new List<DropdownOption>();
            List<DropdownOption> listTolerant = new List<DropdownOption>();

            Payouts openPayouts = Payouts.ForOrganization (transaction.Organization);

            foreach (Payout payout in openPayouts)
            {
                if (payout.AmountCents > -matchAmount*95/100 &&
                         payout.AmountCents < -matchAmount*105/100)
                {
                    string description = String.Format(Resources.Pages.Ledgers.BalanceTransactions_PayoutMatch, payout.Identity,
                        payout.ExpectedTransactionDate, payout.Recipient, payout.Organization.Currency.DisplayCode, payout.AmountCents / 100.0,
                        payout.Specification);

                    if (payout.AmountCents == -matchAmount)
                    {
                        listExact.Add(new DropdownOption
                        {
                            id = payout.Identity.ToString(CultureInfo.InvariantCulture),
                            @group = Resources.Pages.Ledgers.BalanceTransactions_ExactMatches,
                            text = description
                        });
                    }
                    else
                    {
                        listTolerant.Add(new DropdownOption
                        {
                            id = payout.Identity.ToString(CultureInfo.InvariantCulture),
                            @group = Resources.Pages.Ledgers.BalanceTransactions_FivePercentMatches,
                            text = description
                        });
                    }
                }
            }

            result.ExactMatches = listExact.ToArray();
            result.TolerantMatches = listTolerant.ToArray();

            return result;
        }


        private static DropdownOptions GetOpenOutboundInvoiceData(FinancialTransaction transaction)
        {
            DateTime txDateTime = transaction.DateTime;
            Int64 matchAmount = transaction.Rows.AmountCentsTotal;

            DropdownOptions result = new DropdownOptions();

            List<DropdownOption> listExact = new List<DropdownOption>();
            List<DropdownOption> listTolerant = new List<DropdownOption>();

            OutboundInvoices invoices = OutboundInvoices.ForOrganization(transaction.Organization);

            foreach (OutboundInvoice invoice in invoices)
            {
                if (invoice.AmountCents > matchAmount * 95 / 100 &&
                         invoice.AmountCents < matchAmount * 105 / 100)
                {
                    string description = String.Format(Resources.Pages.Ledgers.BalanceTransactions_OutboundInvoiceMatch, invoice.Identity,
                        invoice.CustomerName, invoice.DueDate, invoice.DisplayNativeAmount);

                    if (invoice.HasNativeCurrency)
                    {
                        description += " (" + transaction.Organization.Currency.DisplayCode + " " +
                                       (invoice.AmountCents/100.0).ToString("N2") + ")";
                    }

                    bool invoiceIdMatch = DescriptionContainsInvoiceReference(invoice.Reference, invoice.TheirReference, transaction.Description);


                    if (invoice.AmountCents == matchAmount)
                    {
                        listExact.Add(new DropdownOption
                        {
                            id = invoice.Identity.ToString(CultureInfo.InvariantCulture),
                            @group = invoiceIdMatch? Resources.Pages.Ledgers.BalanceTransactions_MostProbableMatch : Resources.Pages.Ledgers.BalanceTransactions_ExactMatches,
                            text = description
                        });
                    }
                    else
                    {
                        listTolerant.Add(new DropdownOption
                        {
                            id = invoice.Identity.ToString(CultureInfo.InvariantCulture),
                            @group = invoiceIdMatch ? Resources.Pages.Ledgers.BalanceTransactions_MostProbableMatch : Resources.Pages.Ledgers.BalanceTransactions_FivePercentMatches,
                            text = description
                        });
                    }
                }
            }

            result.ExactMatches = listExact.ToArray();
            result.TolerantMatches = listTolerant.ToArray();

            return result;
        }

        private static bool DescriptionContainsInvoiceReference(string ourReference, string theirReference, string description)
        {
            // Remove some noise

            ourReference = ourReference.Replace("-", "").Replace(".", "").Replace("/", "");
            theirReference = theirReference.Replace("-", "").Replace(".", "").Replace("/", "");
            description = description.Replace("-", "").Replace(".", "").Replace("/", "");

            // For every word in the description, check if it's the invoice reference. If so, return true

            string[] descriptionWords = description.Split(' ');

            foreach (string descriptionWord in descriptionWords)
            {
                if (descriptionWord == ourReference)
                {
                    return true;
                }
                if (descriptionWord == theirReference)
                {
                    return true;
                }
            }

            return false;
        }

        [WebMethod]
        public static void BalanceTransactionManually(int transactionId, int accountId)
        {
            if (transactionId == 0 || accountId == 0)
            {
                return;
            }

            AuthenticationData authData = GetAuthenticationDataAndCulture();

            if (
                !authData.Authority.HasAccess(new Access(authData.CurrentOrganization,
                    AccessAspect.BookkeepingDetails)))
            {
                throw new UnauthorizedAccessException();
            }

            FinancialTransaction transaction = FinancialTransaction.FromIdentity(transactionId);
            FinancialAccount account = FinancialAccount.FromIdentity(accountId);
            if (transaction.OrganizationId != authData.CurrentOrganization.Identity || account.OrganizationId != authData.CurrentOrganization.Identity)
            {
                throw new UnauthorizedAccessException();
            }

            transaction.AddRow (account, -transaction.Rows.AmountCentsTotal, authData.CurrentUser);
        }



        [WebMethod]
        public static void MatchTransactionOpenPayout (int transactionId, int payoutId)
        {
            if (transactionId == 0 || payoutId == 0)
            {
                return;
            }

            AuthenticationData authData = GetAuthenticationDataAndCulture();

            if (
                !authData.Authority.HasAccess(new Access(authData.CurrentOrganization,
                    AccessAspect.BookkeepingDetails)))
            {
                throw new UnauthorizedAccessException();
            }

            FinancialTransaction transaction = FinancialTransaction.FromIdentity(transactionId);
            Payout payout = Payout.FromIdentity (payoutId);
            if (transaction.OrganizationId != authData.CurrentOrganization.Identity || payout.OrganizationId != authData.CurrentOrganization.Identity)
            {
                throw new UnauthorizedAccessException();
            }

            if (transaction.Rows.AmountCentsTotal != -payout.AmountCents)
            {
                throw new InvalidOperationException();
            }

            payout.BindToTransactionAndClose (transaction, authData.CurrentUser);

        }



        [WebMethod]
        public static void MatchTransactionOpenPayoutForeign(int transactionId, int payoutId)
        {
            // This is like the non-foreign version except this one chalks up the difference to forex gain/loss accounts

            if (transactionId == 0 || payoutId == 0)
            {
                return;
            }

            AuthenticationData authData = GetAuthenticationDataAndCulture();

            if (
                !authData.Authority.HasAccess(new Access(authData.CurrentOrganization,
                    AccessAspect.BookkeepingDetails)))
            {
                throw new UnauthorizedAccessException();
            }

            FinancialAccount forexSpillAccount =
                authData.CurrentOrganization.FinancialAccounts.IncomeCurrencyFluctuations;

            if (forexSpillAccount == null)
            {
                throw new InvalidOperationException("Need forex gain/loss accounts for this operation");  // TODO: Autocreate?
            }

            FinancialTransaction transaction = FinancialTransaction.FromIdentity(transactionId);
            Payout payout = Payout.FromIdentity(payoutId);

            if (transaction.OrganizationId != authData.CurrentOrganization.Identity || payout.OrganizationId != authData.CurrentOrganization.Identity)
            {
                throw new UnauthorizedAccessException();
            }

            if (-transaction.Rows.AmountCentsTotal > payout.AmountCents)
            {
                // This is a forex loss, not a gain which is the default
                forexSpillAccount = authData.CurrentOrganization.FinancialAccounts.CostsCurrencyFluctuations;
            }

            transaction.AddRow (forexSpillAccount, -(payout.AmountCents + transaction.Rows.AmountCentsTotal), // plus because AmountCentsTotal is negative
                authData.CurrentUser); // Adds the forex adjustment so we can bind payout to tx and close

            if (transaction.Rows.AmountCentsTotal != -payout.AmountCents)
            {
                throw new InvalidOperationException();
            }

            payout.BindToTransactionAndClose(transaction, authData.CurrentUser);

        }

    }
}