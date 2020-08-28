import React from "react";
import PropTypes from "prop-types";
import styled from "styled-components";

import { Text, utils } from "asc-web-components";
const { tablet, mobile } = utils.device;
const StyledBodyAdvantages = styled.div`
  margin-top: 56px;

  display: flex;
  flex-direction: column;
  padding: 32px 32px 32px 0px;
  width: 888px;
  height: 247px;
  position: static;

  background: #f8f9f9;
  border-radius: 6px 6px 0px 0px;

  .payments-icon-enterprise {
    position: absolute;
    margin-left: 666px;
  }
  .first_icon {
    margin: 64px 864px 193px 32px;
  }
  .second_icon {
    margin: 104px 864px 193px 32px;
  }
  .third_icon {
    margin: 144px 864px 193px 32px;
  }
  .four_icon {
    margin: 184px 864px 193px 32px;
  }
  .five_icon {
    margin: 224px 864px 193px 32px;
  }
  .payments-body {
    margin-right: 609px;
    margin-bottom: 22px;
    margin-left: 64px;

    font-weight: bold;
    font-size: 13px;
    color: #555f65;
  }
  .first-advantages {
    margin-top: 37px;
    width: 247px;
  }
  .second-advantages {
    width: 278px;
  }
  .third-advantages {
    width: 124px;
  }
  .four-advantages {
    width: 366px;
  }
  .five-advantages {
    width: 276px;
  }
  .advantages-enterprise {
    width: 856px;
    height: 30px;
    font-weight: bold;
    font-size: 22px;
    line-height: 30px;
    margin: 0px 32px 0 32px;
  }

  @media ${tablet} {
    width: 568px;
    height: 245px;
    .main_icon {
      display: none;
    }
    .advantages-enterprise {
      width: 536px;
    }
  }
  @media ${mobile} {
    margin-top: 16px;
    width: 311px;
    height: 333px;
    .main_icon {
      display: none;
    }
    .advantages-enterprise {
      width: 279px;
      height: 60px;
    }
    .payments-body {
      margin-bottom: 20px;
      line-height: 16px;
    }
  }
`;

const AdvantagesContainer = ({ t }) => {
  return (
    <StyledBodyAdvantages>
      <Text className="advantages-enterprise">{t("SubscriptionGet")}</Text>
      <Text className="payments-body first-advantages">
        {t("OnlyOfficeEditor")}
      </Text>
      <Text className="payments-body second-advantages">
        {t("PrivateRooom")}
      </Text>
      <Text className="payments-body third-advantages">{t("WebEditors")}</Text>
      <Text className="payments-body four-advantages">
        {t("FunctionalityAndSecurity")}
      </Text>
      <Text className="payments-body five-advantages">
        {t("ProfessionalTechSupport")}
      </Text>
      <img
        className="payments-icon-enterprise main_icon"
        src="images/payments_enterprise.svg"
        width="222px"
        height="247px"
        alt="Icon"
      />
      <img
        className="payments-icon-enterprise first_icon"
        src="images/payments_enterprise_icon_first.svg"
        width="24px"
        height="23px"
        alt="Icon"
      />
      <img
        className="payments-icon-enterprise second_icon"
        src="images/payments_enterprise_icon_five.svg"
        width="24px"
        height="23px"
        alt="Icon"
      />

      <img
        className="payments-icon-enterprise 
          third_icon"
        src="images/payments_enterprise_icon_four.svg"
        width="24px"
        height="23px"
        alt="Icon"
      />
      <img
        className="payments-icon-enterprise 
          four_icon"
        src="images/payments_enterprise_icon_second.svg"
        width="24px"
        height="23px"
        alt="Icon"
      />
      <img
        className="payments-icon-enterprise 
          five_icon"
        src="images/payments_enterprise_icon_third.svg"
        width="24px"
        height="23px"
        alt="Icon"
      />
    </StyledBodyAdvantages>
  );
};

AdvantagesContainer.propTypes = {
  t: PropTypes.func.isRequired,
};

export default AdvantagesContainer;