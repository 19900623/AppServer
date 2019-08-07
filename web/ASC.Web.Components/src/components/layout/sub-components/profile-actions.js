import React from 'react'
import PropTypes from 'prop-types'
import Avatar from '../../avatar'
import DropDown from '../../drop-down'
import DropDownItem from '../../drop-down-item'
import DropDownProfileItem from '../../drop-down-profile-item'
import { handleAnyClick } from '../../../utils/event';

class ProfileActions extends React.PureComponent {

  constructor(props) {
    super(props);

    this.ref = React.createRef();

    this.state = {
      opened: props.opened,
      user: props.user,
      userActions: props.userActions
    };

    this.handleClick = this.handleClick.bind(this);
    this.toggle = this.toggle.bind(this);
    this.getUserRole = this.getUserRole.bind(this);
    this.onAvatarClick = this.onAvatarClick.bind(this);
    this.onDropDownItemClick = this.onDropDownItemClick.bind(this);

    if(props.opened)
      handleAnyClick(true, this.handleClick);
  };

  handleClick = (e) => {
    this.state.opened && !this.ref.current.contains(e.target) && this.toggle(false);
  }

  toggle = (opened) => {
    this.setState({ opened: opened });
  }

  componentWillUnmount() {
    handleAnyClick(false, this.handleClick);
  }

  componentDidUpdate(prevProps, prevState) {
    if (this.props.opened !== prevProps.opened) {
      this.toggle(this.props.opened);
    }

    if(this.state.opened !== prevState.opened) {
      handleAnyClick(this.state.opened, this.handleClick);
    }
  }

  getUserRole = (user) => {
    if(user.isOwner) return "owner";
    if(user.isAdmin) return "admin";
    if(user.isVisitor) return "guest";
    return "user";
  };

  onAvatarClick = () => {
    this.toggle(!this.state.opened);
  }

  onDropDownItemClick = (action) => {
    action.onClick && action.onClick();
    this.toggle(!this.state.opened);
  }

  render() {
    console.log("Layout sub-component ProfileActions render");
    return (
      <div ref={this.ref}>
        <Avatar
          size="small"
          role={this.getUserRole(this.state.user)}
          source={this.state.user.avatarSmall}
          userName={this.state.user.displayName}
          onClick={this.onAvatarClick}
        />
        <DropDown
          isUserPreview
          withArrow
          directionX='right'
          isOpen={this.state.opened}
        >
          <DropDownProfileItem
            avatarRole={this.getUserRole(this.state.user)}
            avatarSource={this.state.user.avatarMedium}
            displayName={this.state.user.displayName}
            email={this.state.user.email}
          />
          {
            this.state.userActions.map(action => 
              <DropDownItem 
                {...action}
                onClick={this.onDropDownItemClick.bind(this, action)}
              />
            )
          }
        </DropDown>
      </div>
    );
  }
}

ProfileActions.propTypes = {
  opened: PropTypes.bool,
  user: PropTypes.object,
  userActions: PropTypes.array
}

ProfileActions.defaultProps = {
  opened: false,
  user: {},
  userActions: []
}

export default ProfileActions