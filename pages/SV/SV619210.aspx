<%@ Page Language="C#" MasterPageFile="~/MasterPages/FormDetail.master" AutoEventWireup="true"
    ValidateRequest="false" CodeFile="SV619210.aspx.cs" Inherits="Page_SV619210" %>
<%@ MasterType VirtualPath="~/MasterPages/FormDetail.master" %>

<asp:Content ID="cont1" ContentPlaceHolderID="phDS" runat="Server">
    <px:PXDataSource ID="ds" runat="server" Visible="True" Width="100%"
        TypeName="Serva.Acumatica.SoScannerLabels.ServaLabelHistoryEntry"
        PrimaryView="Filter">
        <CallbackCommands>
            <px:PXDSCallbackCommand Name="Reprint" CommitChanges="True" />
            <px:PXDSCallbackCommand Name="Retry" CommitChanges="True" />
        </CallbackCommands>
    </px:PXDataSource>
</asp:Content>

<asp:Content ID="cont2" ContentPlaceHolderID="phF" runat="Server">
    <px:PXFormView ID="form" runat="server" DataSourceID="ds" DataMember="Filter"
        Width="100%" Caption="Label Reprint / Retry">
        <Template>
            <px:PXLayoutRule ID="rule1" runat="server" StartColumn="True" LabelsWidth="SM" ControlSize="M" />
            <px:PXSelector ID="edShipmentNbr" runat="server" DataField="ShipmentNbr" CommitChanges="True" />
            <px:PXDropDown ID="edStatus" runat="server" DataField="Status" CommitChanges="True" AllowNull="True" />
            <px:PXDateTimeEdit ID="edFromDate" runat="server" DataField="FromDate" CommitChanges="True" />
            <px:PXDateTimeEdit ID="edToDate" runat="server" DataField="ToDate" CommitChanges="True" />
            <px:PXLayoutRule ID="rule2" runat="server" StartColumn="True" LabelsWidth="SM" ControlSize="M" />
            <px:PXNumberEdit ID="edLabelCount" runat="server" DataField="LabelCount" />
            <px:PXTextEdit ID="edReason" runat="server" DataField="Reason" />
            <px:PXSelector ID="edPrinterID" runat="server" DataField="PrinterID" />
        </Template>
    </px:PXFormView>
</asp:Content>

<asp:Content ID="cont3" ContentPlaceHolderID="phG" runat="Server">
    <px:PXGrid ID="grid" runat="server" DataSourceID="ds"
        Width="100%" Height="420px" SkinID="PrimaryInquire" SyncPosition="True">
        <Levels>
            <px:PXGridLevel DataMember="Requests">
                <Columns>
                    <px:PXGridColumn DataField="RequestID" Width="90px" />
                    <px:PXGridColumn DataField="Source" Width="80px" />
                    <px:PXGridColumn DataField="ShipmentNbr" Width="110px" />
                    <px:PXGridColumn DataField="InventoryID" Width="130px" />
                    <px:PXGridColumn DataField="LocationID" Width="110px" />
                    <px:PXGridColumn DataField="PickedQty" Width="90px" />
                    <px:PXGridColumn DataField="QuantityPerLabel" Width="90px" />
                    <px:PXGridColumn DataField="UOM" Width="70px" />
                    <px:PXGridColumn DataField="LabelCount" Width="90px" />
                    <px:PXGridColumn DataField="PrinterName" Width="220px" />
                    <px:PXGridColumn DataField="Status" Width="100px" />
                    <px:PXGridColumn DataField="CreatedDateTime" Width="150px" />
                    <px:PXGridColumn DataField="ErrorMessage" Width="300px" />
                </Columns>
            </px:PXGridLevel>
        </Levels>
    </px:PXGrid>
</asp:Content>
