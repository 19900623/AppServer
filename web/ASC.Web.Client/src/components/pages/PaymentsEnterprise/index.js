import React from "react";
import { PageLayout, utils, store } from "asc-web-common";
import { Loader, utils as Utils, toastr } from "asc-web-components";
import styled from "styled-components";
import { withRouter } from "react-router";
import { connect } from "react-redux";
import { withTranslation } from "react-i18next";
import PropTypes from "prop-types";
import HeaderContainer from "./sub-components/header-container";
import AdvantagesContainer from "./sub-components/advantages-container";
import ButtonContainer from "./sub-components/button-container";
import ContactContainer from "./sub-components/contact-container";
import {
  setLicense,
  getSalesEmail,
  getHelpUrl,
  getBuyUrl,
  getCurrentLicense,
  getSettings,
  getStandalone,
} from "../../../store/payments/actions";
import { createI18N } from "../../../helpers/i18n";

const i18n = createI18N({
  page: "PaymentsEnterprise",
  localesPath: "pages/PaymentsEnterprise",
});
const { setCurrentProductId } = store.auth.actions;
const { changeLanguage } = utils;
const { tablet } = Utils.device;

const StyledBody = styled.div`
  margin: 0 auto;
  width: 920px;

  @media ${tablet} {
    width: 600px;
    margin: 0 auto;
  }
  @media (max-width: 632px) {
    width: 343px;
    margin: 0 auto;
  }
`;

class Body extends React.PureComponent {
  constructor(props) {
    super(props);
    const { t } = this.props;

    this.state = {
      errorMessage: null,
      isErrorLicense: false,
      isLicenseSet: false,
    };

    document.title = `${t("Payments")} – ${t("OrganizationName")}`;
  }

  componentDidMount() {
    const {
      getSalesEmail,
      getHelpUrl,
      getBuyUrl,
      getCurrentLicense,
      getSettings,
      getStandalone,
      currentProductId,
      setCurrentProductId,
    } = this.props;
    currentProductId !== "payments" && setCurrentProductId("payments");
    getSettings();
    // getStandalone();
    getCurrentLicense();
  }

  componentDidUpdate(prevProps) {
    const { currentProductId, getCurrentLicense } = this.props;
    const { isLicenseSet } = this.state;
    if (currentProductId !== prevProps.currentProductId) {
      this.fetchData(currentProductId);
    }
    if (isLicenseSet) {
      // this.props.getStandalone().then(() => {
      //   this.setState({ standAloneMode: this.props.standAloneMode });
      // });
      getCurrentLicense().then(() => {
        this.setState({ isLicenseSet: false });
      });
    }
  }

  onButtonClickUpload = (file) => {
    const { setLicense, t } = this.props;

    let fd = new FormData();
    fd.append("files", file);

    setLicense(null, fd)
      .then(() => {
        toastr.success(t("LoadingLicenseSuccess"), "", 8000, true);
      })
      .catch((error) => {
        toastr.error(t("LoadingLicenseError"), t("LicenseIsNotValid"), 0, true);
        this.setState({
          errorMessage: error,
          isErrorLicense: true,
        });
      })
      .then(() =>
        this.setState({
          isLicenseSet: true,
        })
      );
  };
  onButtonClickBuy = (e) => {
    window.open(e.target.value, "_blank");
  };

  onCloseModalDialog = () => {
    this.setState({
      isVisibleModalDialog: false,
      // errorMessage: null,
    });
  };

  render() {
    const {
      isLoaded,
      salesEmail,
      helpUrl,
      buyUrl,
      expiresDate,
      t,
      culture,
      utcHoursOffset,
      trialMode,
    } = this.props;

    return !isLoaded ? (
      <Loader className="pageLoader" type="rombs" size="40px" />
    ) : (
      <StyledBody>
        <HeaderContainer
          t={t}
          onError={this.onError}
          expiresDate={expiresDate}
          trialMode={trialMode}
          culture={culture}
          utcHoursOffset={utcHoursOffset}
          getExpiresDate={this.getExpiresDate}
        />
        <AdvantagesContainer t={t} />

        <ButtonContainer
          t={t}
          buyUrl={buyUrl}
          onButtonClickBuy={this.onButtonClickBuy}
          onButtonClickUpload={this.onButtonClickUpload}
        />
        <ContactContainer t={t} salesEmail={salesEmail} helpUrl={helpUrl} />
      </StyledBody>
    );
  }
}
const PaymentsWrapper = withTranslation()(Body);
const PaymentsEnterprise = (props) => {
  changeLanguage(i18n);
  return (
    <PageLayout>
      <PageLayout.SectionBody>
        <PaymentsWrapper {...props} i18n={i18n} />
      </PageLayout.SectionBody>
    </PageLayout>
  );
};

PaymentsEnterprise.propTypes = {
  standAloneMode: PropTypes.bool,
  isLoaded: PropTypes.bool,
};

function mapStateToProps(state) {
  return {
    isLoaded: state.auth.isLoaded,
    salesEmail: state.payments.salesEmail,
    helpUrl: state.payments.helpUrl,
    buyUrl: state.payments.buyUrl,
    expiresDate: state.payments.currentLicense.expiresDate,
    trialMode: state.payments.trialMode,
    culture: state.auth.settings.culture,
    utcHoursOffset: state.auth.settings.utcHoursOffset,
  };
}
export default connect(mapStateToProps, {
  setLicense,
  setCurrentProductId,
  // getPortalCultures,
  getSalesEmail,
  getHelpUrl,
  getBuyUrl,
  getCurrentLicense,
  getSettings,
  getStandalone,
})(withRouter(PaymentsEnterprise));