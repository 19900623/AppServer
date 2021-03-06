import {css} from 'styled-components';

const commonTextStyles = css`
  font-family: 'Open Sans', sans-serif, Arial;
  text-align: ${props => props.textAlign};
  color: ${props => props.colorProp};
  ${props => props.truncate && css`
    white-space: nowrap; 
    overflow: hidden; 
    text-overflow: ellipsis;`
  }
`;

export default commonTextStyles;