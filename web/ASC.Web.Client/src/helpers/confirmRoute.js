import React from "react";
import { Route } from "react-router-dom";
import { ValidationResult } from "./../helpers/constants";
import { getObjectByLocation } from "./../helpers/converters";
import { PageLayout, Loader } from "asc-web-components";
import { connect } from "react-redux";
import { withRouter } from "react-router";
import Cookies from "universal-cookie";
import { AUTH_KEY } from "./constants";
import { checkConfirmLink } from "../store/services/api";

class ConfirmRoute extends React.Component {
  constructor(props) {
    super(props);
    this.state = {
      linkData: {},
      isLoaded: false
    };
  }

  componentDidMount() {
    const { forUnauthorized, history } = this.props;

    if (forUnauthorized && new Cookies().get(AUTH_KEY))
      //TODO: Remove cookie getting after setup on server
      return history.push(`/error=Access error`);

    const { location, isAuthenticated } = this.props;
    const { search } = location;

    const queryParams = getObjectByLocation(location);
    const url = location.pathname;
    const posSeparator = url.lastIndexOf("/");
    const type = url.slice(posSeparator + 1);
    const confirmLinkData = Object.assign({ type }, queryParams);

    let path = "";
    if (!isAuthenticated) {
      path = "/login";
    }

    checkConfirmLink(confirmLinkData)
      .then(validationResult => {
        switch (validationResult) {
          case ValidationResult.Ok:
            const confirmHeader = `type=${confirmLinkData.type}&${search.slice(1)}`;
            const linkData = {
              ...confirmLinkData,
              confirmHeader
            };
            this.setState({
              isLoaded: true,
              linkData
            });
            break;
          case ValidationResult.Invalid:
            history.push(`${path}/error=Invalid link`);
            break;
          case ValidationResult.Expired:
            history.push(`${path}/error=Expired link`);
            break;
          default:
            history.push(`${path}/error=Unknown error`);
            break;
        }
      })
      .catch(error => {
        history.push(`${path}/error=${error}`);
      });
  }

  render() {
    const { component: Component, ...rest } = this.props;

    console.log(`ConfirmRoute render`, this.props, this.state);

    return (
      <Route
        {...rest}
        render={props =>
          !this.state.isLoaded ? (
            <PageLayout
              sectionBodyContent={
                <Loader className="pageLoader" type="rombs" size={40} />
              }
            />
          ) : (
            <Component
              {...(props = { ...props, linkData: this.state.linkData })}
            />
          )
        }
      />
    );
  }
}

function mapStateToProps(state) {
  return {
    isAuthenticated: state.auth.isAuthenticated
  };
}

export default connect(
  mapStateToProps,
  { checkConfirmLink }
)(withRouter(ConfirmRoute));