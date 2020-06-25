import React, { Component } from 'react';
import { withRouter } from 'react-router';
import styled from "styled-components";

import { PageLayout } from "asc-web-common";
import { 
  Heading, Text, 
  EmailInput, PasswordInput, 
  InputBlock, Checkbox, Link,
  DropDown, GroupButton, 
  DropDownItem, Button, 
  Box, Loader, utils } from 'asc-web-components';

const { EmailSettings } = utils.email;
const settings = new EmailSettings();
settings.allowDomainPunycode = true;

const settingsPassword = {
  minLength: 6,
  upperCase: true,
  digits: true,
  specSymbols: true
};

const HeaderContent = styled.div`
  display: flex;
  flex-direction: column;

  position: absolute;
  height: 56px;

  background: #0F4071;
  width: 100%;

  .header-logo {
    padding: 0;
    margin: 0;
    position: absolute;
    left: 240px;
    top: 14.5px;
  }
`;

const sectionHeaderContent = <HeaderContent>
  <a className="header-wizard" href="/wizard">
    <img
      className="header-logo"
      src="images/onlyoffice_logo/light_small_general.svg"
      alt="Logo"
    />
  </a>
</HeaderContent>;

const WizardContainer = styled.div`
  position: flex;
  flex-direction: column;

  .form-container {
    width: 960px;
    margin: 0 auto;
    margin-top: 80px;
  }

  .wizard-title {
    width: 960px;
  
    text-align: center;
    font-family: Open Sans;
    font-style: normal;
    font-weight: 600;
    font-size: 32px;
    line-height: 36px;

    margin: 10px 12px;
  }

  .wizard-desc {

    width: 960px;

    text-align: center;
    font-family: Open Sans;
    font-style: normal;
    font-weight: normal;
    font-size: 13px;
    line-height: 20px;

    margin: 10px 12px;
  }

  .wizard-input-email {
    height: 44px;
    width: 311px;
    margin: 34px auto 0 auto;
    font-size: 16px;
    line-height: 22px;
    font-weight: normal;
    font-style: normal;
    font-family: Open Sans;
    padding-left: 16px;
  }

  .wizard-pass {
    width: 360px;
    margin: 16px 0 0 324px;

  }

  .wizard-pass input {
    height: 44px;
    font-family: Open Sans;
    font-style: normal;
    font-weight: normal;
    font-size: 16px;
    line-height: 22px;
    padding-left: 15px;
  }

  .password-tooltip {
    height: 14px;
    width: 311px;
    text-align: left;
    padding: 0;
    margin: 0 auto;

    font-family: Open Sans;
    font-style: normal;
    font-weight: normal;
    font-size: 10px;
    line-height: 14px;
    color: #A3A9AE;
  }

  .input-block {
    width: 311px;
    height: 44px;

    margin: 16px auto 0 auto;
  }

  .input-file {
    display: none;
  }

  .checkbox-container {
    width: 311px;
    margin: 17px auto 0 auto;
  }

  .wizard-checkbox {
    display: inline-block;
  }

  .wizard-checkbox span {
    margin-right: 0.3em;
    vertical-align: middle;
  }

  .link {
    vertical-align: middle;
    font-family: Open Sans;
    font-style: normal;
    font-weight: normal;
    font-size: 13px;
    line-height: 18px;
  }

  .settings {
    width: 311px;
    
    margin: 32px auto 0 auto;
    display: flex;
    flex-direction: row;
    padding: 0;
  }

  .settings-title-block {
    position: static;
    flex: none;
    order: 0;
    align-self: flex-start;
  }

  .settings-title {
    font-family: Open Sans;
    font-style: normal;
    font-weight: normal;
    font-size: 13px;
    line-height: 20px;

    margin: 16px 0px;
  }

  .values {
    position: static;
    flex: none;
    order: 1;
    align-self: flex-start;
    margin: 0;
    padding: 0;
    margin-left: 16px;
  }

  .text, .value {
    font-family: Open Sans;
    font-style: normal;
    font-weight: 600;
    font-size: 13px;
    line-height: 20px;
    padding: 0;
  } 

  .text {
    margin: 16px 0;
  }

  .drop-down {
    display: block;
    margin: 0 0 16px 0;
  }

  .wizard-button {
    display: block;
    width: 311px;
    height: 44px;

    margin: 32px auto 0 auto;
  }
`;

class Body extends Component {
  constructor(props) {
    super(props);

    this.state = {
      password: '',
      isValidPass: false
    }

    this.inputRef = React.createRef();
  }

  isValidPassHandler = (val) => {
    this.state({ isValidPass: val});
  }

  onIconFileClick = () => {
    console.log('click')
    this.inputRef.current.click();
    console.log(this.inputRef.current.value);
  }

  render() {

    return (
      <WizardContainer>
        <Box className="form-container">
          <Heading level={1} title="Wizard" className="wizard-title">
            Welcome to your portal!
          </Heading>
          <Text className="wizard-desc">
            Please setup the portal registration data.
          </Text>
          <EmailInput
            className="wizard-input-email"
            tabIndex={1}
            id="input-email"
            name="email-wizard"
            placeholder={'E-mail'}
            emailSettings={settings}
            onValidateInput={() => console.log('email')}
          />
          <PasswordInput
            tabIndex={2}
            className="wizard-pass"
            id="first"
            inputName="firstPass"
            emailInputName="email-wizard"
            inputWidth="311px"
            inputValue={this.state.password}
            passwordSettings={settingsPassword}
            isDisabled={false}
            placeholder={'Password'}
            onChange={() => console.log('pass onChange')}
            onValidateInput={this.isValidPassHandler}
          />
          <Text className="password-tooltip">
            2-30 characters
          </Text>
          <InputBlock
            className="input-block"
            iconName={"CatalogFolderIcon"}
            onIconClick={this.onIconFileClick}
            onChange={() => console.log('change')}
          >
            <input type="file" className="input-file" ref={this.inputRef}/>
          </InputBlock>
          <Box className="checkbox-container">
            <Checkbox
              className="wizard-checkbox"
              id="license"
              name="confirm"
              label={'Accept the terms of the'}
              isChecked={false}
              isIndeterminate={false}
              isDisabled={false}
              onChange={() => {}}
            />
            <Link 
              className="link"
              type="page" 
              color="#116d9d" 
              href="https://gnu.org/licenses/gpl-3.0.html" 
              isBold={false}
            >License agreements</Link>
          </Box>
          <Box className="settings">
            <Box className="setting-title-block">
              <Text className="settings-title">Domain:</Text>
              <Text className="settings-title">Language:</Text>
              <Text className="settings-title">Time zone:</Text>
            </Box>
            <Box className="values">
              <Text className="text value">portaldomainname.com</Text>
              <GroupButton className="drop-down value" label="English (United States)" isDropdown={true}>
                <DropDownItem 
                  label="English (United States)"
                  onClick={() => console.log('English click')}
                />
                <DropDownItem 
                  label="Русский (Российская Федерация)"
                  onClick={() => console.log('Russia click')}
                />
              </GroupButton>
              <GroupButton className="drop-down value" label="UTC" isDropdown={true}>
                <DropDownItem 
                  label="UTC"
                  onClick={() => console.log('UTC')}
                />
                <DropDownItem 
                  label="Not UTC"
                  onClick={() => console.log('Not UTC')}
                />
              </GroupButton>
            </Box>
          </Box>
          <Button
            className="wizard-button"
            primary
            label={"Continue"}           
            size="big"
            onClick={() => console.log('click btn')}
          />
        </Box>
      </WizardContainer>
    )
  }
}

const Wizard = props => <PageLayout 
  sectionBodyContent={<Body {...props} />} 
  sectionHeaderContent={sectionHeaderContent}
/>;

export default withRouter(Wizard);

/* <GroupButton className="text value" label="English (United States)" isDropdown={true}>
                <DropDownItem 
                  label="English (United States)"
                  onClick={() => console.log('English click')}
                />
                <DropDownItem 
                  label="Русский (Российская Федерация)"
                  onClick={() => console.log('Russia click')}
                />
              </GroupButton>
              <GroupButton className="text value" label="UTC" isDropdown={true}>
                <DropDownItem 
                  label="UTC"
                  onClick={() => console.log('UTC')}
                />
                <DropDownItem 
                  label="Not UTC"
                  onClick={() => console.log('Not UTC')}
                />
              </GroupButton>
              */